using System;
using System.IO;
using AlienDefense.Base;
using AlienDefense.Meta;
using AlienDefense.Save;
using NUnit.Framework;
using UnityEngine;

namespace AlienDefense.Tests.EditMode
{
    /// <summary>Covers the base's timing and transaction rules, which are the parts that cannot be judged by
    /// looking at the screen: offline completion, refusing a second builder, spending exactly once, and
    /// production accruing against a stored timestamp rather than a ticked counter.
    ///
    /// Every test drives the clock by passing an explicit DateTime, which is only possible because the service
    /// never reads DateTime.UtcNow itself.</summary>
    public class BaseProgressionServiceTests
    {
        private const string CentralId = "central";
        private const string MillId = "mill";
        private const string MaterialId = "research_resource";

        private string _testDirectory;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Application.temporaryCachePath, "BaseProgressionTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Test]
        public void Start_WithoutRequirements_IsRefused_AndSpendsNothing()
        {
            PlayerProfileService profile = CreateProfile();
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50));

            // The profile starts with no material at all.
            BaseProgressionService.StartResult result = service.TryStartConstruction(CentralId, Now);

            Assert.AreEqual(BaseProgressionService.StartResult.RequirementsNotMet, result);
            Assert.AreEqual(0, service.GetLevel(CentralId));
            Assert.AreEqual(0, profile.GetItemAmount(MaterialId), "A refused build must not spend anything.");
        }

        [Test]
        public void Start_WithRequirements_SpendsExactlyOnce_AndEntersConstructing()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50));

            BaseProgressionService.StartResult result = service.TryStartConstruction(CentralId, Now);

            Assert.AreEqual(BaseProgressionService.StartResult.Started, result);
            Assert.AreEqual(70, profile.GetItemAmount(MaterialId), "Exactly the requirement should be spent.");
            Assert.AreEqual(BaseBuildingState.Constructing, profile.GetBaseBuilding(CentralId).State);
            Assert.AreEqual(0, service.GetLevel(CentralId), "Level only rises when the build completes.");
        }

        [Test]
        public void Construction_DoesNotCompleteBeforeItsTime()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralSeconds: 60f));
            service.TryStartConstruction(CentralId, Now);

            int completed = service.ResolveFinishedConstructions(Now.AddSeconds(59));

            Assert.AreEqual(0, completed);
            Assert.AreEqual(0, service.GetLevel(CentralId));
        }

        [Test]
        public void Construction_CompletesOffline_WhenTheClockHasPassed()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralSeconds: 60f));
            service.TryStartConstruction(CentralId, Now);

            // Stands in for closing the game and coming back an hour later.
            int completed = service.ResolveFinishedConstructions(Now.AddHours(1));

            Assert.AreEqual(1, completed);
            Assert.AreEqual(1, service.GetLevel(CentralId));
            Assert.AreEqual(BaseBuildingState.Built, profile.GetBaseBuilding(CentralId).State);
        }

        [Test]
        public void Completing_AddsTheLevelsForceReward()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralForce: 100));

            Assert.AreEqual(0, service.ComputeBaseForce());

            service.TryStartConstruction(CentralId, Now);
            service.ResolveFinishedConstructions(Now.AddHours(1));

            Assert.AreEqual(100, service.ComputeBaseForce());
        }

        [Test]
        public void SecondBuild_IsRefused_WhileTheBuilderIsBusy()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 500);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 10, centralSeconds: 600f));

            Assert.AreEqual(BaseProgressionService.StartResult.Started, service.TryStartConstruction(CentralId, Now));
            Assert.AreEqual(BaseProgressionService.StartResult.BuilderBusy, service.TryStartConstruction(MillId, Now));
        }

        [Test]
        public void MaxLevel_RefusesFurtherUpgrades()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 500);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 10, centralSeconds: 1f));

            // The test catalog gives Central a single level, so one build takes it to max.
            service.TryStartConstruction(CentralId, Now);
            service.ResolveFinishedConstructions(Now.AddMinutes(1));

            Assert.AreEqual(BaseProgressionService.StartResult.AlreadyMaxLevel,
                service.TryStartConstruction(CentralId, Now.AddMinutes(2)));
        }

        [Test]
        public void FinishNow_CostsGemsScaledByRemainingTime_AndCompletesTheBuild()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            profile.AddGems(100);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralSeconds: 600f));
            service.TryStartConstruction(CentralId, Now);

            // 10 minutes left at 1 gem per minute.
            int cost = service.GetFinishNowGemCost(CentralId, Now);
            Assert.AreEqual(10, cost);

            Assert.IsTrue(service.TryFinishNow(CentralId, Now));
            Assert.AreEqual(90, profile.Gems);
            Assert.AreEqual(1, service.GetLevel(CentralId));
        }

        [Test]
        public void FinishNow_WithoutEnoughGems_ChangesNothing()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralSeconds: 600f));
            service.TryStartConstruction(CentralId, Now);

            Assert.IsFalse(service.TryFinishNow(CentralId, Now), "No gems were granted, so this must fail.");
            Assert.AreEqual(0, service.GetLevel(CentralId));
            Assert.AreEqual(BaseBuildingState.Constructing, profile.GetBaseBuilding(CentralId).State);
        }

        [Test]
        public void Production_AccruesOffline_AndCollectingBanksItOnce()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseBuildingCatalog catalog = CreateCatalog(centralMaterialCost: 50, centralSeconds: 1f,
                centralProduction: 10, centralIntervalSeconds: 60f, centralStorage: 1000);
            BaseProgressionService service = CreateService(profile, catalog);

            service.TryStartConstruction(CentralId, Now);
            service.ResolveFinishedConstructions(Now.AddSeconds(5));

            catalog.TryGet(CentralId, out BaseBuildingDefinition central);

            // Five minutes after the build finished: five intervals of 10.
            DateTime later = Now.AddSeconds(5).AddMinutes(5);
            int pending = service.GetPendingProduction(central, later, out string materialId);

            Assert.AreEqual(MaterialId, materialId);
            Assert.AreEqual(50, pending);

            int before = profile.GetItemAmount(MaterialId);
            Assert.IsTrue(service.TryCollectProduction(CentralId, later));
            Assert.AreEqual(before + 50, profile.GetItemAmount(MaterialId));

            Assert.AreEqual(0, service.GetPendingProduction(central, later, out _),
                "Collecting must reset the anchor so the same material cannot be banked twice.");
        }

        [Test]
        public void Production_IsCappedByStorage()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseBuildingCatalog catalog = CreateCatalog(centralMaterialCost: 50, centralSeconds: 1f,
                centralProduction: 10, centralIntervalSeconds: 60f, centralStorage: 30);
            BaseProgressionService service = CreateService(profile, catalog);

            service.TryStartConstruction(CentralId, Now);
            service.ResolveFinishedConstructions(Now.AddSeconds(5));
            catalog.TryGet(CentralId, out BaseBuildingDefinition central);

            // A full day would be 1440 without the cap.
            int pending = service.GetPendingProduction(central, Now.AddDays(1), out _);

            Assert.AreEqual(30, pending);
        }

        [Test]
        public void Production_ClockMovedBackwards_ProducesNothing()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseBuildingCatalog catalog = CreateCatalog(centralMaterialCost: 50, centralSeconds: 1f,
                centralProduction: 10, centralIntervalSeconds: 60f, centralStorage: 1000);
            BaseProgressionService service = CreateService(profile, catalog);

            service.TryStartConstruction(CentralId, Now);
            service.ResolveFinishedConstructions(Now.AddSeconds(5));
            catalog.TryGet(CentralId, out BaseBuildingDefinition central);

            int pending = service.GetPendingProduction(central, Now.AddDays(-1), out _);

            Assert.AreEqual(0, pending, "A backwards clock must not mint material.");
        }

        [Test]
        public void SaveRoundTrip_KeepsTheBuildRunning()
        {
            PlayerProfileService profile = CreateProfile();
            profile.AddItem(MaterialId, 120);
            BaseProgressionService service = CreateService(profile, CreateCatalog(centralMaterialCost: 50, centralSeconds: 600f));
            service.TryStartConstruction(CentralId, Now);

            // Re-read the profile the way a relaunch would.
            PlayerProfileService reloaded = CreateProfile();
            BaseBuildingSnapshot saved = reloaded.GetBaseBuilding(CentralId);

            Assert.IsTrue(saved.Exists, "The building row must survive a save/load round trip.");
            Assert.IsTrue(saved.IsBuilding);
            Assert.IsFalse(saved.IsConstructionFinished(Now.AddMinutes(1)));
            Assert.IsTrue(saved.IsConstructionFinished(Now.AddMinutes(20)));
        }

        // ------------------------------------------------------------------ Harness

        private static DateTime Now => new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private PlayerProfileService CreateProfile()
        {
            var repository = new SaveFileRepository(_testDirectory);
            var saveService = new SaveService(repository);
            PlayerProfileSaveData data = saveService.LoadOrCreateDefault(null, "L1");
            return new PlayerProfileService(saveService, data);
        }

        private static BaseProgressionService CreateService(PlayerProfileService profile, BaseBuildingCatalog catalog)
        {
            return new BaseProgressionService(profile, catalog, null);
        }

        /// <summary>Builds a two-building catalog in memory. Definitions are ScriptableObjects, so the fields are
        /// set through SerializedObject-free reflection-free helpers: CreateInstance plus JsonUtility overwrite,
        /// which is the least brittle way to author one without an .asset file.</summary>
        private static BaseBuildingCatalog CreateCatalog(int centralMaterialCost, float centralSeconds = 2f,
            int centralForce = 0, int centralProduction = 0, float centralIntervalSeconds = 60f,
            int centralStorage = 100)
        {
            string centralJson = BuildDefinitionJson(CentralId, "Central", centralMaterialCost, centralSeconds,
                centralForce, centralProduction, centralIntervalSeconds, centralStorage);
            string millJson = BuildDefinitionJson(MillId, "Mill", 10, 2f, 0, 0, 60f, 100);

            var central = ScriptableObject.CreateInstance<BaseBuildingDefinition>();
            JsonUtility.FromJsonOverwrite(centralJson, central);

            var mill = ScriptableObject.CreateInstance<BaseBuildingDefinition>();
            JsonUtility.FromJsonOverwrite(millJson, mill);

            var catalog = ScriptableObject.CreateInstance<BaseBuildingCatalog>();
            JsonUtility.FromJsonOverwrite("{\"_maxConcurrentConstruction\":1,\"_centralBuildingId\":\"" + CentralId +
                "\",\"_finishNowGemsPerMinute\":1.0}", catalog);

            // The catalog's array holds object references, which JSON cannot carry - assign it directly.
            var serialized = new UnityEditor.SerializedObject(catalog);
            UnityEditor.SerializedProperty buildings = serialized.FindProperty("_buildings");
            buildings.arraySize = 2;
            buildings.GetArrayElementAtIndex(0).objectReferenceValue = central;
            buildings.GetArrayElementAtIndex(1).objectReferenceValue = mill;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return catalog;
        }

        private static string BuildDefinitionJson(string id, string name, int materialCost, float seconds,
            int force, int production, float intervalSeconds, int storage)
        {
            string requirement = materialCost > 0
                ? "{\"_type\":3,\"_targetId\":\"" + MaterialId + "\",\"_requiredValue\":" + materialCost + "}"
                : string.Empty;

            return "{" +
                   "\"_id\":\"" + id + "\"," +
                   "\"_displayName\":\"" + name + "\"," +
                   "\"_plotId\":\"plot_" + id + "\"," +
                   "\"_levels\":[{" +
                   "\"_level\":1," +
                   "\"_constructionSeconds\":" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"_forceReward\":" + force + "," +
                   "\"_requirements\":[" + requirement + "]," +
                   "\"_production\":{" +
                   "\"_materialId\":\"" + (production > 0 ? MaterialId : string.Empty) + "\"," +
                   "\"_amountPerInterval\":" + production + "," +
                   "\"_intervalSeconds\":" + intervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                   "\"_storageCapacity\":" + storage +
                   "}}]}";
        }
    }
}

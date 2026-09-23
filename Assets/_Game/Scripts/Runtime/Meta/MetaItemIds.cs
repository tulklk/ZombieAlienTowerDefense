namespace AlienDefense.Meta
{
    /// <summary>Stable string ids for meta inventory items. Keep in sync with MetaItemCatalog assets.</summary>
    public static class MetaItemIds
    {
        public const string Microcircuit = "microcircuit";
        public const string VoltDrip = "volt_drip";
        public const string CardMortar = "card_mortar";
        public const string CardIceDagger = "card_ice_dagger";
        public const string CardTurret = "card_turret";
        public const string CardTesla = "card_tesla";
        public const string CardUfo = "card_ufo";

        /// <summary>The Blaster Tower's upgrade card, kept under the id the card had before the tower was named.</summary>
        public const string CardBlaster = CardTurret;

        /// <summary>The Frost Tower's upgrade card, kept under the id the card had before the tower was named.</summary>
        public const string CardFrost = CardIceDagger;

        public const string ReactorBlueprint = "reactor_blueprint";
        public const string AntiGravityBlueprint = "anti_gravity_blueprint";
        public const string ResearchResource = "research_resource";
        public const string RareAtom = "rare_atom";
    }
}

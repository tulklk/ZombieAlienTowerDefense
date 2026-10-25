using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Geometry of the generated Level_02 kit - the pieces the project has no art for: cottages in six colour
    /// schemes, a church, market stalls, a water tower, the dark enemy barn with open doors, a roofed stone-well
    /// canopy, mailbox, signpost, bench, clothes line, wagon wheel, scarecrow and a fire-truck ladder. Stylised
    /// low-poly, one colour per face, local origin on the ground, front facing +Z.</summary>
    public static partial class Level02Builder
    {
        private struct HouseScheme
        {
            public Color Wall, Roof, RoofAlt, Trim, Door, Glass, Shutter, Chimney;
            public bool Thatch;
        }

        private static readonly Color Stone = Hex("#8E897E");
        private static readonly Color StoneLight = Hex("#A8A296");
        private static readonly Color Glass = Hex("#6FA6D2");

        private static HouseScheme Scheme(int variant)
        {
            switch (variant % 6)
            {
                case 0: return new HouseScheme { Wall = Hex("#EBA65B"), Roof = Hex("#C4513B"), RoofAlt = Hex("#AD4633"), Trim = Hex("#F5E9D0"), Door = Hex("#7A4A2A"), Glass = Glass, Shutter = Hex("#5E8C4A"), Chimney = Hex("#9C5A45") };
                case 1: return new HouseScheme { Wall = Hex("#93BE6F"), Roof = Hex("#4D5264"), RoofAlt = Hex("#434758"), Trim = Hex("#F7F3E8"), Door = Hex("#A8452F"), Glass = Glass, Shutter = Hex("#F0E3C2"), Chimney = Hex("#8C5442") };
                case 2: return new HouseScheme { Wall = Hex("#F0E4C6"), Roof = Hex("#B9432F"), RoofAlt = Hex("#A33B2A"), Trim = Hex("#7E5637"), Door = Hex("#3F6F9F"), Glass = Glass, Shutter = Hex("#4C7DB4"), Chimney = Hex("#9A5B43") };
                case 3: return new HouseScheme { Wall = Hex("#F2D06E"), Roof = Hex("#CDA753"), RoofAlt = Hex("#B9933F"), Trim = Hex("#8A5B36"), Door = Hex("#6C4428"), Glass = Glass, Shutter = Hex("#B8573C"), Chimney = Hex("#8E6A4E"), Thatch = true };
                case 4: return new HouseScheme { Wall = Hex("#CD5A42"), Roof = Hex("#E28B3E"), RoofAlt = Hex("#CB7A33"), Trim = Hex("#F5EFE4"), Door = Hex("#5B3B23"), Glass = Glass, Shutter = Hex("#F5EFE4"), Chimney = Hex("#7F7A73") };
                default: return new HouseScheme { Wall = Hex("#A5C9DE"), Roof = Hex("#D8733A"), RoofAlt = Hex("#C4652F"), Trim = Hex("#F4F1EA"), Door = Hex("#8B5631"), Glass = Hex("#5E93C4"), Shutter = Hex("#3F6C9A"), Chimney = Hex("#9C5A45") };
            }
        }

        // ------------------------------------------------------------------ house

        private static Mesh DesignHouse(int variant)
        {
            HouseScheme s = Scheme(variant);
            var m = new KitMesh();
            float w = 5.0f + (variant % 3) * 0.35f;
            float d = 4.3f + (variant % 2) * 0.45f;
            float h = 2.9f + (variant % 2) * 0.25f;
            const float found = 0.35f;
            float top = found + h;

            m.Box(new Vector3(0f, found * 0.5f, 0f), new Vector3(w + 0.26f, found, d + 0.26f), Stone, StoneLight);
            m.Box(new Vector3(0f, found + h * 0.5f, 0f), new Vector3(w, h, d), s.Wall);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    m.Box(new Vector3(sx * (w * 0.5f), found + h * 0.5f, sz * (d * 0.5f)), new Vector3(0.22f, h, 0.22f), s.Trim);
                }
            }

            m.Box(new Vector3(0f, top - 0.07f, 0f), new Vector3(w + 0.12f, 0.14f, d + 0.12f), s.Trim);

            GableRoof(m, w, d, top, s.Thatch ? 0.62f : 0.55f, s.Roof, s.RoofAlt, s.Wall, s.Thatch);

            // Door (offset for some variants) with frame and stone step.
            float doorX = variant % 2 == 0 ? 0f : -w * 0.2f;
            float front = d * 0.5f;
            m.Box(new Vector3(doorX, found + 1.0f, front + 0.03f), new Vector3(1.18f, 2.02f, 0.07f), s.Trim);
            m.Box(new Vector3(doorX, found + 0.92f, front + 0.06f), new Vector3(0.9f, 1.84f, 0.08f), s.Door);
            m.Box(new Vector3(doorX + 0.3f, found + 0.95f, front + 0.12f), new Vector3(0.08f, 0.08f, 0.06f), Hex("#E2C35A"));
            m.Box(new Vector3(doorX, found * 0.5f, front + 0.42f), new Vector3(1.4f, found, 0.62f), StoneLight);

            // Windows: front (two, or one opposite an offset door), sides, back.
            float wy = found + 1.55f;
            if (Mathf.Approximately(doorX, 0f))
            {
                Window(m, new Vector3(-w * 0.3f, wy, front), Vector3.forward, 0.78f, 0.82f, s, true);
                Window(m, new Vector3(w * 0.3f, wy, front), Vector3.forward, 0.78f, 0.82f, s, true);
            }
            else
            {
                Window(m, new Vector3(w * 0.22f, wy, front), Vector3.forward, 1.1f, 0.82f, s, true);
            }

            Window(m, new Vector3(w * 0.5f, wy, 0f), Vector3.right, 0.72f, 0.78f, s, false);
            Window(m, new Vector3(-w * 0.5f, wy, 0f), Vector3.left, 0.72f, 0.78f, s, false);
            Window(m, new Vector3(-w * 0.25f, wy, -front), Vector3.back, 0.72f, 0.78f, s, false);
            Window(m, new Vector3(w * 0.25f, wy, -front), Vector3.back, 0.72f, 0.78f, s, false);

            // Chimney on the back slope.
            if (!s.Thatch || variant % 2 == 1)
            {
                float rise = d * 0.55f;
                m.Box(new Vector3(w * 0.27f, top + rise * 0.55f, -d * 0.17f), new Vector3(0.55f, rise * 1.1f + 0.4f, 0.55f), s.Chimney);
                m.Box(new Vector3(w * 0.27f, top + rise * 0.55f + rise * 0.55f + 0.25f, -d * 0.17f), new Vector3(0.7f, 0.12f, 0.7f), Hex("#4F4A45"));
            }

            // Porch canopy for a couple of schemes.
            if (variant == 1 || variant == 4)
            {
                float pz = front + 0.75f;
                m.Box(new Vector3(doorX - 0.75f, found + 1.1f, pz), new Vector3(0.12f, 2.2f, 0.12f), s.Trim);
                m.Box(new Vector3(doorX + 0.75f, found + 1.1f, pz), new Vector3(0.12f, 2.2f, 0.12f), s.Trim);
                m.Box(new Vector3(doorX, found + 2.3f, front + 0.4f), new Vector3(1.9f, 0.12f, 1.0f), Quaternion.Euler(12f, 0f, 0f), s.Roof);
            }

            return m.ToMesh("SM_Level02_House_" + variant);
        }

        /// <summary>Gable roof with the ridge along X, overhanging eaves, tile rows in two tones, gable ends and a
        /// ridge cap. Thatch = thicker, rounder slabs.</summary>
        private static void GableRoof(KitMesh m, float w, float d, float top, float pitch, Color roof, Color alt, Color gable, bool thatch)
        {
            float rise = d * pitch;
            const float overhang = 0.38f;
            float half = d * 0.5f + overhang;
            float slopeLength = Mathf.Sqrt(half * half + (rise + overhang * pitch) * (rise + overhang * pitch));
            float angle = Mathf.Atan2(rise + overhang * pitch, half) * Mathf.Rad2Deg;
            float thickness = thatch ? 0.32f : 0.16f;
            float width = w + 0.62f;
            const int rows = 5;

            foreach (float side in new[] { 1f, -1f })
            {
                // Slab frame: origin at the ridge, slope running down toward +side Z.
                m.Push(new Vector3(0f, top + rise, 0f), Quaternion.Euler(angle, side > 0f ? 0f : 180f, 0f));
                for (int r = 0; r < rows; r++)
                {
                    float z0 = slopeLength * r / rows;
                    float z1 = slopeLength * (r + 1) / rows;
                    Color c = r % 2 == 0 ? roof : alt;
                    float bump = thatch ? 0f : (r % 2) * 0.03f;
                    m.Box(new Vector3(0f, thickness * 0.5f + bump, (z0 + z1) * 0.5f), new Vector3(width, thickness, z1 - z0 + 0.02f), c);
                }

                m.Pop();
            }

            // Gable walls.
            foreach (float sx in new[] { -1f, 1f })
            {
                var pts = new[]
                {
                    new Vector3(sx * w * 0.5f, top, -d * 0.5f),
                    new Vector3(sx * w * 0.5f, top, d * 0.5f),
                    new Vector3(sx * w * 0.5f, top + rise - 0.05f, 0f),
                };
                m.Polygon(pts, gable, new Vector3(sx, 0f, 0f));
            }

            m.Box(new Vector3(0f, top + rise + thickness * 0.6f, 0f), new Vector3(width + 0.05f, thatch ? 0.28f : 0.16f, thatch ? 0.5f : 0.3f), Shade(alt, 0.9f));
        }

        private static void Window(KitMesh m, Vector3 center, Vector3 facing, float width, float height, HouseScheme s, bool shutters)
        {
            Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
            m.Push(center, rotation);
            m.Box(new Vector3(0f, 0f, 0.03f), new Vector3(width + 0.18f, height + 0.18f, 0.06f), s.Trim);
            m.Box(new Vector3(0f, 0f, 0.06f), new Vector3(width, height, 0.06f), s.Glass);
            m.Box(new Vector3(0f, 0f, 0.1f), new Vector3(width, 0.05f, 0.04f), s.Trim);
            m.Box(new Vector3(0f, 0f, 0.1f), new Vector3(0.05f, height, 0.04f), s.Trim);
            m.Box(new Vector3(0f, -height * 0.5f - 0.1f, 0.1f), new Vector3(width + 0.34f, 0.08f, 0.2f), s.Trim);
            if (shutters)
            {
                m.Box(new Vector3(-(width * 0.5f + 0.24f), 0f, 0.05f), new Vector3(0.3f, height + 0.05f, 0.05f), s.Shutter);
                m.Box(new Vector3(width * 0.5f + 0.24f, 0f, 0.05f), new Vector3(0.3f, height + 0.05f, 0.05f), s.Shutter);
                // flower box
                m.Box(new Vector3(0f, -height * 0.5f - 0.24f, 0.16f), new Vector3(width + 0.1f, 0.2f, 0.22f), Hex("#8A5A35"));
                for (int i = 0; i < 4; i++)
                {
                    float x = -width * 0.38f + i * width * 0.25f;
                    Color bloom = i % 2 == 0 ? Hex("#E0525C") : Hex("#F2C14E");
                    m.Box(new Vector3(x, -height * 0.5f - 0.1f, 0.18f), new Vector3(0.14f, 0.12f, 0.14f), bloom);
                }
            }

            m.Pop();
        }

        // ------------------------------------------------------------------ church

        private static Mesh DesignChurch()
        {
            var m = new KitMesh();
            Color wall = Hex("#F2EADB");
            Color trim = Hex("#D8CAAE");
            Color roof = Hex("#474B59");
            Color roofAlt = Hex("#3E4250");
            Color door = Hex("#6E4B31");
            const float w = 6.2f;
            const float d = 10f;
            const float h = 4.6f;
            const float found = 0.4f;
            float top = found + h;

            m.Box(new Vector3(0f, found * 0.5f, 0f), new Vector3(w + 0.3f, found, d + 0.3f), Stone, StoneLight);
            m.Box(new Vector3(0f, found + h * 0.5f, 0f), new Vector3(w, h, d), wall);
            m.Box(new Vector3(0f, top - 0.08f, 0f), new Vector3(w + 0.14f, 0.16f, d + 0.14f), trim);

            // Nave roof: ridge along Z (rotate the house roof helper by 90 degrees).
            m.Push(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            GableRoof(m, d, w, top, 0.62f, roof, roofAlt, wall, false);
            m.Pop();

            // Tall side windows with pointed tops.
            for (int i = 0; i < 3; i++)
            {
                float z = -d * 0.3f + i * d * 0.3f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    m.Push(new Vector3(sx * w * 0.5f, found + 2.4f, z), Quaternion.LookRotation(new Vector3(sx, 0f, 0f)));
                    m.Box(new Vector3(0f, 0f, 0.03f), new Vector3(0.85f, 1.95f, 0.06f), trim);
                    m.Box(new Vector3(0f, 0f, 0.06f), new Vector3(0.62f, 1.7f, 0.06f), Hex("#7FB0DA"));
                    m.Polygon(new[] { new Vector3(-0.42f, 0.97f, 0.05f), new Vector3(0.42f, 0.97f, 0.05f), new Vector3(0f, 1.45f, 0.05f) }, trim, Vector3.forward);
                    m.Pop();
                }
            }

            // Bell tower at the front (+Z).
            const float tw = 2.8f;
            const float th = 8f;
            float tz = d * 0.5f + 0.6f;
            m.Box(new Vector3(0f, found * 0.5f, tz), new Vector3(tw + 0.3f, found, tw + 0.3f), Stone, StoneLight);
            m.Box(new Vector3(0f, found + th * 0.5f, tz), new Vector3(tw, th, tw), wall);
            m.Box(new Vector3(0f, found + th - 0.1f, tz), new Vector3(tw + 0.2f, 0.2f, tw + 0.2f), trim);
            m.Box(new Vector3(0f, found + th * 0.62f, tz), new Vector3(tw + 0.12f, 0.14f, tw + 0.12f), trim);
            // Belfry openings.
            foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 c = new Vector3(0f, found + th - 1.1f, tz) + dir * (tw * 0.5f + 0.01f);
                m.Push(c, Quaternion.LookRotation(dir));
                m.Box(Vector3.zero, new Vector3(0.9f, 1.3f, 0.04f), Hex("#2E2A2A"));
                m.Box(new Vector3(0f, -0.7f, 0.05f), new Vector3(1.1f, 0.1f, 0.12f), trim);
                m.Pop();
            }

            // Spire and cross.
            m.Cylinder(new Vector3(0f, found + th, tz), tw * 0.78f, 0f, 4.4f, 4, i => i % 2 == 0 ? roof : roofAlt, null, null, Mathf.PI * 0.25f);
            Color gold = Hex("#E3C15A");
            float spireTop = found + th + 4.4f;
            m.Box(new Vector3(0f, spireTop + 0.45f, tz), new Vector3(0.12f, 0.95f, 0.12f), gold);
            m.Box(new Vector3(0f, spireTop + 0.62f, tz), new Vector3(0.55f, 0.12f, 0.12f), gold);

            // Arched door + rose window on the tower front.
            float fz = tz + tw * 0.5f;
            m.Box(new Vector3(0f, found + 1.25f, fz + 0.03f), new Vector3(1.7f, 2.5f, 0.06f), trim);
            m.Box(new Vector3(0f, found + 1.2f, fz + 0.06f), new Vector3(1.35f, 2.35f, 0.07f), door);
            var arch = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * i / 6f;
                arch[i] = new Vector3(Mathf.Cos(a) * 0.675f, found + 2.37f + Mathf.Sin(a) * 0.55f, fz + 0.06f);
            }

            m.Polygon(arch, door, Vector3.forward);
            var rose = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f;
                rose[i] = new Vector3(Mathf.Cos(a) * 0.55f, found + 4.4f + Mathf.Sin(a) * 0.55f, fz + 0.05f);
            }

            m.Polygon(rose, Hex("#7FB0DA"), Vector3.forward);
            m.Box(new Vector3(0f, found * 0.25f, fz + 0.9f), new Vector3(2.4f, found * 0.5f, 0.7f), StoneLight);
            return m.ToMesh("SM_Level02_Church");
        }

        // ------------------------------------------------------------------ market stall

        private static Mesh DesignMarketStall(int variant)
        {
            var m = new KitMesh();
            Color stripe = variant == 1 ? Hex("#D2493C") : Hex("#3E7FCC");
            Color white = variant == 2 ? Hex("#F3E6C4") : Hex("#F5F5F0");
            Color wood = Hex("#A8743F");
            Color woodDark = Hex("#8E5F33");

            m.Box(new Vector3(0f, 0.47f, 0.25f), new Vector3(2.8f, 0.94f, 0.95f), wood);
            m.Box(new Vector3(0f, 0.98f, 0.25f), new Vector3(3.0f, 0.08f, 1.15f), woodDark);
            for (int i = 0; i < 5; i++)
            {
                m.Box(new Vector3(-1.1f + i * 0.55f, 0.47f, 0.735f), new Vector3(0.44f, 0.8f, 0.03f), i % 2 == 0 ? Shade(wood, 1.08f) : wood);
            }

            foreach (float sx in new[] { -1.42f, 1.42f })
            {
                m.Box(new Vector3(sx, 1.25f, 0.85f), new Vector3(0.12f, 2.5f, 0.12f), woodDark);
                m.Box(new Vector3(sx, 1.42f, -0.6f), new Vector3(0.12f, 2.84f, 0.12f), woodDark);
            }

            // Striped sloping canopy with a scalloped valance.
            Vector3 back = new Vector3(0f, 2.86f, -0.8f);
            Vector3 frontEdge = new Vector3(0f, 2.44f, 1.12f);
            float slope = Vector3.Distance(back, frontEdge);
            float tilt = Mathf.Atan2(back.y - frontEdge.y, frontEdge.z - back.z) * Mathf.Rad2Deg;
            m.Push((back + frontEdge) * 0.5f, Quaternion.Euler(tilt, 0f, 0f));
            const int stripes = 8;
            const float width = 3.4f;
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -width * 0.5f + width * i / stripes;
                m.Box(new Vector3(x0 + width / stripes * 0.5f, 0f, 0f), new Vector3(width / stripes + 0.005f, 0.06f, slope), i % 2 == 0 ? stripe : white);
            }

            m.Pop();
            for (int i = 0; i < stripes * 2; i++)
            {
                float x0 = -width * 0.5f + width * i / (stripes * 2);
                float x1 = x0 + width / (stripes * 2);
                Color c = (i / 2) % 2 == 0 ? stripe : white;
                var tri = new[] { new Vector3(x0, frontEdge.y, frontEdge.z + 0.02f), new Vector3(x1, frontEdge.y, frontEdge.z + 0.02f), new Vector3((x0 + x1) * 0.5f, frontEdge.y - 0.26f, frontEdge.z + 0.02f) };
                m.Polygon(tri, c, Vector3.forward);
                m.Polygon(tri, c, Vector3.back);
            }

            // Goods on the counter.
            Color[] goods = { Hex("#E8812F"), Hex("#D9453A"), Hex("#88B846"), Hex("#F2C94C") };
            for (int i = 0; i < 6; i++)
            {
                float x = -1.1f + i * 0.44f;
                m.Box(new Vector3(x, 1.1f, 0.3f), new Vector3(0.36f, 0.16f, 0.5f), woodDark);
                m.Box(new Vector3(x, 1.24f, 0.3f), new Vector3(0.28f, 0.14f, 0.4f), goods[(i + variant) % goods.Length]);
            }

            return m.ToMesh("SM_Level02_MarketStall_" + variant);
        }

        // ------------------------------------------------------------------ water tower

        private static Mesh DesignWaterTower()
        {
            var m = new KitMesh();
            Color wood = Hex("#8B5A33");
            Color woodDark = Hex("#744A2A");
            Color band = Hex("#4F5459");
            const float legTop = 7.4f;

            var baseCorners = new[] { new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f), new Vector3(2f, 0f, 2f), new Vector3(-2f, 0f, 2f) };
            var topCorners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                topCorners[i] = new Vector3(baseCorners[i].x * 0.68f, legTop, baseCorners[i].z * 0.68f);
                m.Beam(baseCorners[i] + Vector3.down * 0.3f, topCorners[i], 0.3f, wood);
                m.Box(baseCorners[i] + Vector3.up * 0.15f, new Vector3(0.6f, 0.3f, 0.6f), Stone, StoneLight);
            }

            foreach (float y in new[] { 2.3f, 4.9f })
            {
                float t = y / legTop;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = Vector3.Lerp(baseCorners[i], topCorners[i], t);
                    Vector3 b = Vector3.Lerp(baseCorners[(i + 1) % 4], topCorners[(i + 1) % 4], t);
                    m.Beam(a, b, 0.18f, woodDark);
                }
            }

            for (int i = 0; i < 4; i += 2)
            {
                Vector3 a0 = baseCorners[i];
                Vector3 b0 = baseCorners[(i + 1) % 4];
                Vector3 a1 = Vector3.Lerp(baseCorners[i], topCorners[i], 4.9f / legTop);
                Vector3 b1 = Vector3.Lerp(baseCorners[(i + 1) % 4], topCorners[(i + 1) % 4], 4.9f / legTop);
                m.Beam(a0 + Vector3.up * 0.4f, b1, 0.14f, woodDark);
                m.Beam(b0 + Vector3.up * 0.4f, a1, 0.14f, woodDark);
            }

            // Ladder up one side.
            Vector3 l0 = new Vector3(-0.3f, 0f, 2.05f);
            Vector3 l1 = new Vector3(-0.3f, legTop, 1.45f);
            Vector3 r0 = l0 + Vector3.right * 0.6f;
            Vector3 r1 = l1 + Vector3.right * 0.6f;
            m.Beam(l0, l1, 0.08f, band);
            m.Beam(r0, r1, 0.08f, band);
            for (int i = 1; i < 18; i++)
            {
                float t = i / 18f;
                m.Beam(Vector3.Lerp(l0, l1, t), Vector3.Lerp(r0, r1, t), 0.05f, band);
            }

            // Platform, tank with staves and bands, conical roof.
            m.Cylinder(new Vector3(0f, legTop, 0f), 2.35f, 2.35f, 0.25f, 8, _ => Hex("#946038"), Hex("#A36B3F"), Hex("#6E4527"));
            const float tankBase = legTop + 0.25f;
            const float tankHeight = 3.3f;
            m.Cylinder(new Vector3(0f, tankBase, 0f), 1.95f, 1.95f, tankHeight, 14, i => i % 2 == 0 ? Hex("#AD733F") : Hex("#9C6536"), null, Hex("#7A4E2C"));
            foreach (float y in new[] { 0.35f, 1.6f, 2.85f })
            {
                m.Cylinder(new Vector3(0f, tankBase + y, 0f), 2.01f, 2.01f, 0.13f, 14, _ => band, null);
            }

            m.Cylinder(new Vector3(0f, tankBase + tankHeight, 0f), 2.3f, 0f, 1.75f, 14, i => i % 2 == 0 ? Hex("#B84A3A") : Hex("#A54133"), null, Hex("#6E2E26"));
            m.Cylinder(new Vector3(0f, tankBase + tankHeight + 1.7f, 0f), 0.14f, 0.14f, 0.35f, 6, _ => band, band);
            return m.ToMesh("SM_Level02_WaterTower");
        }

        // ------------------------------------------------------------------ enemy barn

        private static Mesh DesignEnemyBarn()
        {
            var m = new KitMesh();
            Color wall = Hex("#5E4A39");
            Color batten = Hex("#4A3A2D");
            Color trim = Hex("#7A6450");
            Color roof = Hex("#3D3A40");
            Color roofAlt = Hex("#35323A");
            Color inside = Hex("#17120F");
            const float w = 10f;
            const float d = 12f;
            const float h = 5f;
            const float doorW = 5.4f;
            const float doorH = 4.3f;
            const float t = 0.25f;

            // Gambrel section (x, y) points for the gable ends.
            var gable = new[] { new Vector2(-w * 0.5f, h), new Vector2(-3.3f, 7.3f), new Vector2(0f, 8.7f), new Vector2(3.3f, 7.3f), new Vector2(w * 0.5f, h) };

            // Side and back walls (boxes -> both faces; the inner faces get an inside lining below).
            m.Box(new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall);
            m.Box(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall);
            m.Box(new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, t), wall);
            // Front wall around the door opening.
            float side = (w - doorW) * 0.5f;
            m.Box(new Vector3(-(doorW * 0.5f + side * 0.5f), h * 0.5f, d * 0.5f), new Vector3(side, h, t), wall);
            m.Box(new Vector3(doorW * 0.5f + side * 0.5f, h * 0.5f, d * 0.5f), new Vector3(side, h, t), wall);
            m.Box(new Vector3(0f, doorH + (h - doorH) * 0.5f, d * 0.5f), new Vector3(doorW, h - doorH, t), wall);

            // Battens on the outside of every wall.
            for (float x = -w * 0.5f + 0.5f; x < w * 0.5f; x += 1f)
            {
                if (Mathf.Abs(x) > doorW * 0.5f + 0.1f)
                {
                    m.Box(new Vector3(x, h * 0.5f, d * 0.5f + 0.15f), new Vector3(0.12f, h, 0.06f), batten);
                }

                m.Box(new Vector3(x, h * 0.5f, -d * 0.5f - 0.15f), new Vector3(0.12f, h, 0.06f), batten);
            }

            for (float z = -d * 0.5f + 0.5f; z < d * 0.5f; z += 1f)
            {
                m.Box(new Vector3(w * 0.5f + 0.15f, h * 0.5f, z), new Vector3(0.06f, h, 0.12f), batten);
                m.Box(new Vector3(-w * 0.5f - 0.15f, h * 0.5f, z), new Vector3(0.06f, h, 0.12f), batten);
            }

            // Door frame trims.
            m.Box(new Vector3(-doorW * 0.5f, doorH * 0.5f, d * 0.5f + 0.16f), new Vector3(0.2f, doorH, 0.08f), trim);
            m.Box(new Vector3(doorW * 0.5f, doorH * 0.5f, d * 0.5f + 0.16f), new Vector3(0.2f, doorH, 0.08f), trim);
            m.Box(new Vector3(0f, doorH, d * 0.5f + 0.16f), new Vector3(doorW + 0.2f, 0.2f, 0.08f), trim);

            // Gable ends (front and back), outside faces + dark inside faces.
            foreach (float sz in new[] { 1f, -1f })
            {
                float z = sz * d * 0.5f;
                var outer = new Vector3[gable.Length];
                var inner = new Vector3[gable.Length];
                for (int i = 0; i < gable.Length; i++)
                {
                    outer[i] = new Vector3(gable[i].x, gable[i].y, z + sz * 0.12f);
                    inner[i] = new Vector3(gable[i].x, gable[i].y, z - sz * 0.12f);
                }

                m.Polygon(outer, wall, new Vector3(0f, 0f, sz));
                m.Polygon(inner, inside, new Vector3(0f, 0f, -sz));
            }

            // Hayloft door on the front gable.
            m.Box(new Vector3(0f, 6.4f, d * 0.5f + 0.16f), new Vector3(1.6f, 1.5f, 0.06f), trim);
            m.Box(new Vector3(0f, 6.4f, d * 0.5f + 0.2f), new Vector3(1.3f, 1.2f, 0.06f), inside);

            // Gambrel roof: four slabs along Z with two-tone plank rows.
            for (int i = 0; i < gable.Length - 1; i++)
            {
                Vector2 a = gable[i];
                Vector2 b = gable[i + 1];
                Vector2 dir = (b - a).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                float length = Vector2.Distance(a, b) + 0.1f;
                Vector2 mid = (a + b) * 0.5f + normal * 0.1f;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                const int rows = 6;
                for (int r = 0; r < rows; r++)
                {
                    float z0 = -d * 0.5f - 0.45f + (d + 0.9f) * r / rows;
                    float z1 = -d * 0.5f - 0.45f + (d + 0.9f) * (r + 1) / rows;
                    m.Box(new Vector3(mid.x, mid.y, (z0 + z1) * 0.5f), new Vector3(length, 0.2f, z1 - z0 + 0.01f), Quaternion.Euler(0f, 0f, angle), r % 2 == 0 ? roof : roofAlt);
                }
            }

            // Inside lining (dark) so the open doorway reads as a deep shadowed interior.
            m.Quad(new Vector3(-w * 0.5f + 0.13f, 0.2f, -d * 0.5f + 0.13f), new Vector3(w * 0.5f - 0.13f, 0.2f, -d * 0.5f + 0.13f),
                new Vector3(w * 0.5f - 0.13f, 0.2f, d * 0.5f - 0.13f), new Vector3(-w * 0.5f + 0.13f, 0.2f, d * 0.5f - 0.13f), inside, Vector3.up);
            m.Quad(new Vector3(-w * 0.5f + 0.13f, 0f, -d * 0.5f + 0.13f), new Vector3(w * 0.5f - 0.13f, 0f, -d * 0.5f + 0.13f),
                new Vector3(w * 0.5f - 0.13f, h, -d * 0.5f + 0.13f), new Vector3(-w * 0.5f + 0.13f, h, -d * 0.5f + 0.13f), inside, Vector3.forward);
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * (w * 0.5f - 0.13f);
                m.Quad(new Vector3(x, 0f, -d * 0.5f), new Vector3(x, 0f, d * 0.5f), new Vector3(x, h, d * 0.5f), new Vector3(x, h, -d * 0.5f), inside, new Vector3(-sx, 0f, 0f));
            }

            for (int i = 0; i < gable.Length - 1; i++)
            {
                Vector2 a = gable[i];
                Vector2 b = gable[i + 1];
                m.Quad(new Vector3(a.x, a.y - 0.05f, -d * 0.5f), new Vector3(b.x, b.y - 0.05f, -d * 0.5f), new Vector3(b.x, b.y - 0.05f, d * 0.5f), new Vector3(a.x, a.y - 0.05f, d * 0.5f), inside, Vector3.down);
            }

            // Door leaves swung open outward, one hanging a little crooked.
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 hinge = new Vector3(sx * doorW * 0.5f, 0f, d * 0.5f + 0.2f);
                m.Push(hinge, Quaternion.Euler(0f, sx * 118f, sx > 0f ? 2.5f : 0f));
                float half = doorW * 0.25f;
                m.Box(new Vector3(-sx * half, doorH * 0.5f, 0f), new Vector3(doorW * 0.5f, doorH - 0.1f, 0.14f), Hex("#57432F"));
                Vector3 p0 = new Vector3(-sx * 0.2f, 0.4f, 0.1f);
                Vector3 p1 = new Vector3(-sx * (doorW * 0.5f - 0.2f), doorH - 0.5f, 0.1f);
                m.Beam(p0, p1, 0.16f, trim);
                m.Box(new Vector3(-sx * half, 0.4f, 0.1f), new Vector3(doorW * 0.5f - 0.1f, 0.16f, 0.08f), trim);
                m.Box(new Vector3(-sx * half, doorH - 0.5f, 0.1f), new Vector3(doorW * 0.5f - 0.1f, 0.16f, 0.08f), trim);
                m.Pop();
            }

            // Cow skull over the door.
            Color bone = Hex("#EDE6D3");
            Vector3 skull = new Vector3(0f, doorH + 0.42f, d * 0.5f + 0.3f);
            m.Box(skull, new Vector3(0.62f, 0.5f, 0.28f), bone);
            m.Box(skull + new Vector3(0f, -0.38f, 0.02f), new Vector3(0.36f, 0.34f, 0.24f), bone);
            m.Box(skull + new Vector3(-0.15f, 0.04f, 0.13f), new Vector3(0.14f, 0.14f, 0.05f), Hex("#1B1714"));
            m.Box(skull + new Vector3(0.15f, 0.04f, 0.13f), new Vector3(0.14f, 0.14f, 0.05f), Hex("#1B1714"));
            m.Box(skull + new Vector3(-0.5f, 0.2f, 0f), new Vector3(0.5f, 0.12f, 0.12f), Quaternion.Euler(0f, 0f, 28f), bone);
            m.Box(skull + new Vector3(0.5f, 0.2f, 0f), new Vector3(0.5f, 0.12f, 0.12f), Quaternion.Euler(0f, 0f, -28f), bone);

            // Loose planks leaning on the walls.
            m.Box(new Vector3(-w * 0.5f - 0.5f, 1.2f, 2f), new Vector3(0.1f, 2.6f, 0.4f), Quaternion.Euler(0f, 0f, 14f), trim);
            m.Box(new Vector3(w * 0.5f + 0.55f, 1.1f, -1.5f), new Vector3(0.1f, 2.4f, 0.35f), Quaternion.Euler(0f, 0f, -16f), batten);
            return m.ToMesh("SM_Level02_EnemyBarn");
        }

        // ------------------------------------------------------------------ small kit pieces

        private static Mesh DesignWellCanopy(bool thatch)
        {
            var m = new KitMesh();
            Color wood = Hex("#7F5230");
            Color roof = thatch ? Hex("#CFA852") : Hex("#D2703C");
            Color roofAlt = thatch ? Hex("#BA933F") : Hex("#BE6233");
            m.Box(new Vector3(-0.98f, 1.05f, 0f), new Vector3(0.14f, 2.1f, 0.14f), wood);
            m.Box(new Vector3(0.98f, 1.05f, 0f), new Vector3(0.14f, 2.1f, 0.14f), wood);
            m.Box(new Vector3(0f, 1.95f, 0f), new Vector3(2.1f, 0.12f, 0.12f), wood);
            GableRoof(m, 2.1f, 1.3f, 2.02f, 0.62f, roof, roofAlt, wood, thatch);
            m.Box(new Vector3(0f, 1.45f, 0f), new Vector3(0.02f, 0.9f, 0.02f), Hex("#3B3530"));
            m.Cylinder(new Vector3(0f, 0.85f, 0f), 0.16f, 0.2f, 0.28f, 8, _ => Hex("#8B5A33"), Hex("#4B7FB0"));
            return m.ToMesh("SM_Level02_WellCanopy");
        }

        private static Mesh DesignMailbox(int variant)
        {
            var m = new KitMesh();
            Color box = variant == 0 ? Hex("#3E6FB4") : Hex("#C8453A");
            m.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.1f), Hex("#7F5230"));
            m.Box(new Vector3(0f, 1.2f, 0.05f), new Vector3(0.3f, 0.28f, 0.52f), box);
            m.Box(new Vector3(0.19f, 1.36f, 0.12f), new Vector3(0.03f, 0.24f, 0.05f), Hex("#E24A3B"));
            m.Box(new Vector3(0.19f, 1.46f, 0.18f), new Vector3(0.03f, 0.08f, 0.14f), Hex("#E24A3B"));
            return m.ToMesh("SM_Level02_Mailbox_" + variant);
        }

        private static Mesh DesignSignpost()
        {
            var m = new KitMesh();
            Color wood = Hex("#8A5A35");
            Color board = Hex("#C3935F");
            m.Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 1.9f, 0.12f), wood);
            m.Push(new Vector3(0.35f, 1.62f, 0f), Quaternion.Euler(0f, -12f, 0f));
            m.Box(Vector3.zero, new Vector3(0.95f, 0.24f, 0.05f), board);
            m.Polygon(new[] { new Vector3(0.475f, 0.12f, 0f), new Vector3(0.475f, -0.12f, 0f), new Vector3(0.64f, 0f, 0f) }, board, Vector3.forward);
            m.Box(new Vector3(-0.05f, 0f, 0.03f), new Vector3(0.6f, 0.04f, 0.02f), Hex("#6B4428"));
            m.Pop();
            m.Push(new Vector3(-0.3f, 1.3f, 0.02f), Quaternion.Euler(0f, 160f, 0f));
            m.Box(Vector3.zero, new Vector3(0.85f, 0.22f, 0.05f), Shade(board, 0.92f));
            m.Polygon(new[] { new Vector3(0.425f, 0.11f, 0f), new Vector3(0.425f, -0.11f, 0f), new Vector3(0.58f, 0f, 0f) }, Shade(board, 0.92f), Vector3.forward);
            m.Pop();
            return m.ToMesh("SM_Level02_Signpost");
        }

        private static Mesh DesignBench()
        {
            var m = new KitMesh();
            Color wood = Hex("#A06E42");
            Color iron = Hex("#3F3D3B");
            for (int i = 0; i < 3; i++)
            {
                m.Box(new Vector3(0f, 0.46f, -0.14f + i * 0.14f), new Vector3(1.6f, 0.06f, 0.12f), wood);
            }

            m.Box(new Vector3(0f, 0.78f, -0.26f), new Vector3(1.6f, 0.12f, 0.05f), Quaternion.Euler(-12f, 0f, 0f), wood);
            m.Box(new Vector3(0f, 0.95f, -0.29f), new Vector3(1.6f, 0.12f, 0.05f), Quaternion.Euler(-12f, 0f, 0f), wood);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                m.Box(new Vector3(x, 0.23f, 0.12f), new Vector3(0.06f, 0.46f, 0.06f), iron);
                m.Box(new Vector3(x, 0.5f, -0.24f), new Vector3(0.06f, 1f, 0.06f), iron);
            }

            return m.ToMesh("SM_Level02_Bench");
        }

        private static Mesh DesignClothesLine()
        {
            var m = new KitMesh();
            Color wood = Hex("#7F5230");
            foreach (float x in new[] { -2f, 2f })
            {
                m.Box(new Vector3(x, 1.0f, 0f), new Vector3(0.1f, 2f, 0.1f), wood);
                m.Box(new Vector3(x, 1.95f, 0f), new Vector3(0.1f, 0.1f, 0.7f), wood);
            }

            m.Box(new Vector3(0f, 1.9f, 0.25f), new Vector3(4f, 0.025f, 0.025f), Hex("#E9E4D6"));
            Color[] cloth = { Hex("#F1EEE6"), Hex("#5B8BD0"), Hex("#D65A4A"), Hex("#EBC94F"), Hex("#7DB36A") };
            float x0 = -1.55f;
            for (int i = 0; i < cloth.Length; i++)
            {
                float w = 0.5f + (i % 3) * 0.1f;
                float h = 0.55f + (i % 2) * 0.25f;
                m.Box(new Vector3(x0 + w * 0.5f, 1.9f - h * 0.5f, 0.25f), new Vector3(w, h, 0.03f), cloth[i]);
                x0 += w + 0.12f;
            }

            return m.ToMesh("SM_Level02_ClothesLine");
        }

        private static Mesh DesignWagonWheel()
        {
            var m = new KitMesh();
            Color wood = Hex("#8A5A33");
            Color iron = Hex("#4A4744");
            const int segments = 12;
            const float r = 0.56f;
            // Wheel stands in the XY plane (axle along Z), centre at r above the ground.
            Vector3 c = new Vector3(0f, r, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * r;
                Vector3 p1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * r;
                m.Beam(p0, p1, 0.11f, i % 2 == 0 ? wood : Shade(wood, 0.9f));
            }

            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f;
                m.Beam(c, c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (r - 0.05f), 0.06f, wood);
            }

            m.Box(c, new Vector3(0.2f, 0.2f, 0.18f), iron);
            return m.ToMesh("SM_Level02_WagonWheel");
        }

        private static Mesh DesignScarecrow()
        {
            var m = new KitMesh();
            Color wood = Hex("#7F5230");
            Color straw = Hex("#E3C66A");
            m.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), wood);
            m.Box(new Vector3(0f, 1.55f, 0f), new Vector3(1.7f, 0.1f, 0.1f), wood);
            m.Box(new Vector3(0f, 1.35f, 0f), new Vector3(0.72f, 0.72f, 0.32f), Hex("#C8513E"));
            m.Box(new Vector3(0f, 1.35f, 0.17f), new Vector3(0.72f, 0.08f, 0.02f), Hex("#8E2F24"));
            m.Box(new Vector3(0f, 1.35f, 0.17f), new Vector3(0.08f, 0.72f, 0.02f), Hex("#8E2F24"));
            m.Box(new Vector3(0f, 1.52f, 0f), new Vector3(1.4f, 0.24f, 0.26f), Hex("#C8513E"));
            foreach (float x in new[] { -0.78f, 0.78f })
            {
                m.Box(new Vector3(x, 1.5f, 0f), new Vector3(0.18f, 0.16f, 0.16f), straw);
            }

            m.Box(new Vector3(0f, 0.82f, 0f), new Vector3(0.6f, 0.4f, 0.28f), Hex("#4E6FA6"));
            m.Box(new Vector3(0f, 1.95f, 0f), new Vector3(0.38f, 0.38f, 0.36f), straw);
            m.Box(new Vector3(-0.08f, 1.98f, 0.19f), new Vector3(0.07f, 0.07f, 0.02f), Hex("#2B2622"));
            m.Box(new Vector3(0.08f, 1.98f, 0.19f), new Vector3(0.07f, 0.07f, 0.02f), Hex("#2B2622"));
            m.Cylinder(new Vector3(0f, 2.14f, 0f), 0.42f, 0.42f, 0.05f, 10, _ => Hex("#8A6236"), Hex("#9A7040"));
            m.Cylinder(new Vector3(0f, 2.19f, 0f), 0.22f, 0.1f, 0.34f, 10, _ => Hex("#8A6236"), Hex("#9A7040"));
            return m.ToMesh("SM_Level02_Scarecrow");
        }

        /// <summary>Ladder + light bar that turn the red flatbed Truck into a fire truck. Placed relative to the
        /// truck's own bounds by the caller.</summary>
        private static Mesh DesignFireLadder(float length)
        {
            var m = new KitMesh();
            Color metal = Hex("#C9CDD2");
            Color dark = Hex("#6B6F75");
            foreach (float x in new[] { -0.35f, 0.35f })
            {
                m.Box(new Vector3(x, 0f, 0f), new Vector3(0.08f, 0.12f, length), metal);
            }

            for (int i = 0; i <= 12; i++)
            {
                m.Box(new Vector3(0f, 0.02f, -length * 0.5f + length * i / 12f), new Vector3(0.7f, 0.05f, 0.05f), metal);
            }

            m.Box(new Vector3(0f, -0.25f, length * 0.35f), new Vector3(0.3f, 0.4f, 0.3f), dark);
            m.Box(new Vector3(0f, -0.25f, -length * 0.4f), new Vector3(0.9f, 0.35f, 0.35f), dark);
            return m.ToMesh("SM_Level02_FireLadder");
        }

        private static Mesh DesignLightBar()
        {
            var m = new KitMesh();
            m.Box(new Vector3(-0.35f, 0f, 0f), new Vector3(0.5f, 0.16f, 0.25f), Hex("#E03A2E"));
            m.Box(new Vector3(0.35f, 0f, 0f), new Vector3(0.5f, 0.16f, 0.25f), Hex("#3A6FE0"));
            m.Box(new Vector3(0f, -0.1f, 0f), new Vector3(1.3f, 0.06f, 0.3f), Hex("#2F3033"));
            return m.ToMesh("SM_Level02_LightBar");
        }
    }
}

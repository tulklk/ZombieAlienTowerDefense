using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>Deterministic hash/value noise for the Level_02 generators (terrain paint, cliff jitter, texture
    /// synthesis). Same input -> same output on every run, so rebuilding the level reproduces it exactly.</summary>
    public static partial class Level02Builder
    {
        private static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Hash1(int i, int seed) => Hash(i, i * 31 + 7, seed);

        private static float Smooth01(float t) => t * t * (3f - 2f * t);

        /// <summary>Value noise in [0,1]. With <paramref name="period"/> &gt; 0 it tiles every period cells.</summary>
        private static float ValueNoise(float x, float y, int seed, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = Smooth01(x - x0);
            float fy = Smooth01(y - y0);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            if (period > 0)
            {
                x0 = ((x0 % period) + period) % period;
                y0 = ((y0 % period) + period) % period;
                x1 = ((x1 % period) + period) % period;
                y1 = ((y1 % period) + period) % period;
            }

            float a = Hash(x0, y0, seed);
            float b = Hash(x1, y0, seed);
            float c = Hash(x0, y1, seed);
            float d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float x, float y, int seed, int octaves = 3, int period = 0)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(x, y, seed + o * 17, period) * amplitude;
                total += amplitude;
                x *= 2f;
                y *= 2f;
                period *= 2;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static Color Shade(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);
    }
}

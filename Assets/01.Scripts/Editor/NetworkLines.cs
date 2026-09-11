using UnityEngine;

namespace TrainSudoku.Editor
{
    /// <summary>
    /// The twenty lines added after the four that shipped: their identity, their colour, and the nine stations on
    /// each. Data only — <see cref="NetworkGenerator"/> turns a row of this into boards and assets.
    /// </summary>
    /// <remarks>
    /// <b>Names are invented proper nouns and are never translated (D14)</b>, so none of this reaches the String
    /// Table and adding a line costs nothing in localisation. Each line keeps to one register — brewing, quarrying,
    /// birds, herbs — so a station reads as belonging somewhere rather than as a name from a bag.
    ///
    /// <b>The order of this table is the order the player earns the lines in</b>, because
    /// <c>GameFlow.IsLineUnlocked</c> opens line <i>n</i> off line <i>n-1</i> and the flat level list is the save
    /// file's identity. Append to it; never insert or reorder.
    ///
    /// <b>Colours obey three rules</b>, which matter more than the exact values: mid lightness, so nothing washes
    /// out against <c>Palette.Paper</c> or reads as <c>Palette.Closed</c>; real saturation, so a line at full colour
    /// can never be mistaken for the 25%-toward-paper tint a locked line is drawn in; and enough hue between any two
    /// lines that share a piece of map. <c>LineColourTests</c> holds the first two.
    /// </remarks>
    public static class NetworkLines
    {
        public sealed class LineSpec
        {
            public string Id;
            public string Code;
            public string Name;
            public Color Colour;
            public string[] Stations;
        }

        public static readonly LineSpec[] All =
        {
            new LineSpec
            {
                Id = "hb", Code = "HB", Name = "Havenbrook", Colour = new Color32(0x1E, 0x8F, 0x86, 0xFF),
                Stations = new[]
                {
                    "Quayhead", "Netherwharf", "Tidebourne",
                    "Coblestrand", "Marrowquay", "Saltcombe",
                    "Harborgate", "Winchmoor", "Havenbrook Pier",
                },
            },
            new LineSpec
            {
                Id = "sm", Code = "SM", Name = "Saltmarch", Colour = new Color32(0xC0, 0x39, 0x2B, 0xFF),
                Stations = new[]
                {
                    "Brinefoot", "Marshlode", "Saltergate",
                    "Reedhaven", "Pallowfen", "Crabtree Halt",
                    "Sedgewick", "Lowmarch", "Saltmarch End",
                },
            },
            new LineSpec
            {
                Id = "av", Code = "AV", Name = "Ashenvale", Colour = new Color32(0x7B, 0x5B, 0xC4, 0xFF),
                Stations = new[]
                {
                    "Ashfold", "Birchgate", "Elmhurst Green",
                    "Rowanlea", "Hollowpine", "Thornbeck",
                    "Yewdale", "Coppicewood", "Ashenvale Grange",
                },
            },
            new LineSpec
            {
                Id = "gh", Code = "GH", Name = "Goldhollow", Colour = new Color32(0xC9, 0xA2, 0x27, 0xFF),
                Stations = new[]
                {
                    "Sinkshaft", "Pennyhollow", "Gildercross",
                    "Oreford", "Bullionhaugh", "Nuggetmoor",
                    "Assayers Lane", "Chasmgate", "Goldhollow Deep",
                },
            },
            new LineSpec
            {
                Id = "vc", Code = "VC", Name = "Vellacourt", Colour = new Color32(0x3B, 0x4A, 0x9C, 0xFF),
                Stations = new[]
                {
                    "Almsgate", "Chancelwick", "Vellamead",
                    "Regents Cross", "Palatine Row", "Cloistergate",
                    "Heraldsfield", "Coronet Hill", "Vellacourt Palace",
                },
            },
            new LineSpec
            {
                Id = "bw", Code = "BW", Name = "Briarwharf", Colour = new Color32(0x5C, 0x8A, 0x3C, 0xFF),
                Stations = new[]
                {
                    "Thistledock", "Bramblequay", "Nettleford",
                    "Sloeberry", "Hawthorn Reach", "Briarlock",
                    "Rushbourne", "Tanglewharf", "Briarwharf Basin",
                },
            },
            new LineSpec
            {
                Id = "dm", Code = "DM", Name = "Duskmere", Colour = new Color32(0x8E, 0x3A, 0x62, 0xFF),
                Stations = new[]
                {
                    "Gloamford", "Eventide", "Shadowmere",
                    "Umbergate", "Nightjar Halt", "Vesperfield",
                    "Duskhollow", "Lanternwick", "Duskmere Last",
                },
            },
            new LineSpec
            {
                Id = "kg", Code = "KG", Name = "Kestrelgate", Colour = new Color32(0x2E, 0x9B, 0xD6, 0xFF),
                Stations = new[]
                {
                    "Falconridge", "Merlinhaugh", "Harriers Cross",
                    "Osprey Reach", "Kitewood", "Peregrine Hill",
                    "Sparrowgate", "Buzzardmoor", "Kestrelgate Eyrie",
                },
            },
            new LineSpec
            {
                Id = "mg", Code = "MG", Name = "Maltongrove", Colour = new Color32(0x7A, 0x8B, 0x2B, 0xFF),
                Stations = new[]
                {
                    "Hopfield", "Barleycross", "Mashhouse",
                    "Tunwick", "Coopersgate", "Draymoor",
                    "Yeastbourne", "Caskhollow", "Maltongrove Brewery",
                },
            },
            new LineSpec
            {
                Id = "pm", Code = "PM", Name = "Pellamar", Colour = new Color32(0xE2, 0x65, 0x4B, 0xFF),
                Stations = new[]
                {
                    "Foamgate", "Lullwater", "Pellacove",
                    "Sirenhaugh", "Deepstrand", "Coralmoor",
                    "Brackenshoal", "Tidewrack", "Pellamar Point",
                },
            },
            new LineSpec
            {
                Id = "qf", Code = "QF", Name = "Quarrowfield", Colour = new Color32(0x4A, 0x6A, 0x8F, 0xFF),
                Stations = new[]
                {
                    "Chiselgate", "Grithaven", "Sparstone",
                    "Flintmoor", "Quarrowcross", "Hewersfield",
                    "Slatecombe", "Riggwell", "Quarrowfield Face",
                },
            },
            new LineSpec
            {
                Id = "rc", Code = "RC", Name = "Ravenscar", Colour = new Color32(0x8C, 0x2F, 0x45, 0xFF),
                Stations = new[]
                {
                    "Corbiegate", "Crowhollow", "Scarfoot",
                    "Rookmoor", "Blackfeather", "Craghaven",
                    "Gibbetstone", "Ravensdeep", "Ravenscar Summit",
                },
            },
            new LineSpec
            {
                Id = "sh", Code = "SH", Name = "Stonehythe", Colour = new Color32(0x2F, 0x8F, 0x5B, 0xFF),
                Stations = new[]
                {
                    "Cairnfoot", "Dolmengate", "Menhirwood",
                    "Kerbstone", "Lintelhaugh", "Cobblemoor",
                    "Quernfield", "Barrowhythe", "Stonehythe Keep",
                },
            },
            new LineSpec
            {
                Id = "tb", Code = "TB", Name = "Thistlebourne", Colour = new Color32(0x9B, 0x7F, 0xD4, 0xFF),
                Stations = new[]
                {
                    "Burrgate", "Tuftmoor", "Spinehollow",
                    "Prickwood", "Thistledown", "Cardoon Halt",
                    "Bristlemere", "Barbfield", "Thistlebourne Head",
                },
            },
            new LineSpec
            {
                Id = "uc", Code = "UC", Name = "Undercliffe", Colour = new Color32(0xA6, 0x52, 0x2C, 0xFF),
                Stations = new[]
                {
                    "Ledgegate", "Overhangs", "Scarpwood",
                    "Talusmoor", "Undermere", "Cleftbourne",
                    "Precipice Row", "Shalefield", "Undercliffe Base",
                },
            },
            new LineSpec
            {
                Id = "wx", Code = "WX", Name = "Wexmoor", Colour = new Color32(0x1F, 0xA2, 0xA6, 0xFF),
                Stations = new[]
                {
                    "Heatherlow", "Peatgate", "Tussockmere",
                    "Bogbean Halt", "Whinfield", "Gorsecombe",
                    "Wexcross", "Sphagnum Hill", "Wexmoor Waste",
                },
            },
            new LineSpec
            {
                Id = "yd", Code = "YD", Name = "Yarrowdene", Colour = new Color32(0xB9, 0x8A, 0x2E, 0xFF),
                Stations = new[]
                {
                    "Sorrelgate", "Comfrey Cross", "Feverwick",
                    "Yarrowmead", "Balmhollow", "Tansyfield",
                    "Wormwood Halt", "Angelica Row", "Yarrowdene Physic",
                },
            },
            new LineSpec
            {
                Id = "zm", Code = "ZM", Name = "Zephyrmoor", Colour = new Color32(0x17, 0x86, 0x9B, 0xFF),
                Stations = new[]
                {
                    "Gustgate", "Windlass Hill", "Bellowmere",
                    "Squallfield", "Zephyrhaugh", "Weathervane",
                    "Draughtwood", "Galeford", "Zephyrmoor Crest",
                },
            },
            new LineSpec
            {
                Id = "im", Code = "IM", Name = "Ironmere", Colour = new Color32(0x5A, 0x73, 0x91, 0xFF),
                Stations = new[]
                {
                    "Slaggate", "Puddlefield", "Anvilcross",
                    "Wroughtmoor", "Bloomeryhaugh", "Rivetwick",
                    "Forgemere", "Ingotstone", "Ironmere Works",
                },
            },
            new LineSpec
            {
                Id = "lv", Code = "LV", Name = "Larkspur Vale", Colour = new Color32(0xB4, 0x40, 0x9B, 0xFF),
                Stations = new[]
                {
                    "Delphine Gate", "Lupinfield", "Foxglove Row",
                    "Larkspur Cross", "Campanula Halt", "Monkshood Moor",
                    "Peony Hollow", "Aster Reach", "Larkspur Vale End",
                },
            },
        };
    }
}

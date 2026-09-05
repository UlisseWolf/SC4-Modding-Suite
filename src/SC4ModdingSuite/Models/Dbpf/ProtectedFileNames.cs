using System;
using System.Collections.Generic;
using System.IO;

namespace SC4ModdingSuite.Models;

/// <summary>
/// The core SC4 game data files. Editing and re-saving these in place is extremely risky
/// (corrupting one can break the whole game installation or an active region), so the app
/// allows opening them - e.g. to inspect building/prop/audio entries - but always forces
/// "Salva con nome..." instead of an in-place "Salva", regardless of where on disk a file
/// with one of these names happens to be found.
/// </summary>
public static class ProtectedFileNames
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "SimCity_1.dat",
        "SimCity_2.dat",
        "SimCity_3.dat",
        "SimCity_4.dat",
        "SimCity_5.dat",
        "SimCityLocale.dat",
    };

    public static bool IsProtected(string? path) =>
        path is not null && Names.Contains(Path.GetFileName(path));
}

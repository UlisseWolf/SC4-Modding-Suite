using System;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Generates a random Group/Instance ID exactly the way Ilive Reader's own TGI generator
/// does (<c>CDlgGenerator::OnGenerate</c>, <c>reader/DlgGenerator.cpp</c>):
///
/// <code>
/// GUID guid;
/// CoCreateGuid(&amp;guid);
/// csText.Format("%08x", guid.Data1);   // -&gt; Group
/// CoCreateGuid(&amp;guid);
/// csText.Format("%08x", guid.Data1);   // -&gt; Instance
/// </code>
///
/// i.e. it does <b>not</b> use a pseudo-random number generator at all - it creates a
/// fresh Windows GUID and takes only its first 32-bit field (<c>Data1</c>), once per ID
/// (Group and Instance each get their own independently-generated GUID, not two halves of
/// the same one). <see cref="System.Guid"/> in .NET is deliberately binary-compatible with
/// the native Win32 <c>GUID</c> struct for COM interop, so <c>Guid.NewGuid().ToByteArray()</c>'s
/// first 4 bytes are numerically identical to what <c>CoCreateGuid</c>'s <c>Data1</c> field
/// would contain for an equivalent GUID - this reproduces Ilive Reader's exact behavior
/// (and its statistical properties: cryptographically/system-random per .NET's own
/// <see cref="Guid.NewGuid"/> implementation, effectively collision-free, with no explicit
/// exclusion of any value including 0 - matching the original, which has none either).
///
/// This app previously relied on csDBPF's own <c>TGI(t, g, i)</c> constructor to
/// auto-generate a value when Group/Instance was passed as 0 (documented behavior, see
/// <c>csDBPF.xml</c>), but csDBPF ships as a compiled DLL with no available source, so its
/// exact algorithm can't be confirmed to match Ilive Reader's GUID-based approach - and
/// given a real algorithmic difference (GUID-derived vs. whatever csDBPF does internally)
/// was exactly what was being asked to verify, this class now generates the value itself
/// and passes an already-nonzero result to the TGI constructor instead.
/// </summary>
public static class TgiGenerator
{
    public static uint GenerateRandomId()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return BitConverter.ToUInt32(bytes, 0);
    }
}

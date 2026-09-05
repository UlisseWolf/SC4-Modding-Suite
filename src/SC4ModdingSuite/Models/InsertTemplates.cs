using System.Collections.Generic;
using csDBPF;

namespace SC4ModdingSuite.Models;

public sealed record InsertTemplate(string Name, TGI Tgi, byte[] Bytes);

/// <summary>
/// The two blank entry templates Ilive Reader ships under Template/ (exemplar.eqz,
/// cohort.cqz), listed in its Template/template.xml and inserted via "Insert Template"
/// (ID_MENU_INSERT_TEMPLATE, DlgTemplate) - a quick way to add a blank Exemplar or Cohort
/// entry to build from instead of typing one out from scratch. Bytes and Type/Group below
/// are copied byte-for-byte from those two files (exemplar.eqz is QFS-compressed exactly
/// as Ilive Reader shipped it; cohort.cqz is not - <see cref="DbpfService.AddNewEntryRaw"/>
/// stores either as-is). Instance is randomized fresh at insert time rather than reusing
/// the template's own placeholder Instance (0), so inserting the same template twice - or
/// into a file that already has an entry at Instance 0 - never collides.
/// </summary>
public static class InsertTemplates
{
    public static readonly IReadOnlyList<InsertTemplate> All = new[]
    {
        new InsertTemplate(
            "Exemplar",
            new TGI(0x6534284A, 0x07BDDF1C, 0x00000000),
            new byte[]
            {
                0x2A, 0x00, 0x00, 0x00, 0x10, 0xFB, 0x00, 0x00, 0x25, 0xE1, 0x45, 0x51, 0x5A, 0x42, 0x31, 0x23,
                0x23, 0x23, 0x87, 0x40, 0x00, 0x00, 0x01, 0x03, 0x01, 0x05, 0x08, 0x20, 0xE1, 0x0C, 0x80, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0xFC,
            }),
        new InsertTemplate(
            "Cohort",
            new TGI(0x05342861, 0x00000000, 0x00000000),
            new byte[]
            {
                0x43, 0x51, 0x5A, 0x42, 0x31, 0x23, 0x23, 0x23, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            }),
    };
}

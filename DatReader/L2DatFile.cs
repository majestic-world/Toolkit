using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace L2Toolkit.DatReader;

/// <summary>
/// High-level reader for Lineage 2 .dat files.
/// Handles decryption and binary parsing using the Fafurion (Classic Secret of Empire) structure.
/// </summary>
public sealed class L2DatFile
{
    private readonly string[] _nameTable;

    /// <summary>
    /// Creates a reader without L2GameDataName resolution (MAP_INT returns raw index).
    /// </summary>
    public L2DatFile()
    {
        _nameTable = [];
    }

    /// <summary>
    /// Creates a reader with L2GameDataName table loaded from a decrypted .dat file.
    /// </summary>
    public L2DatFile(string gameDataNameDatPath)
    {
        _nameTable = LoadNameTable(gameDataNameDatPath);
    }

    /// <summary>
    /// Creates a reader with a pre-loaded name table.
    /// </summary>
    public L2DatFile(string[] nameTable)
    {
        _nameTable = nameTable;
    }

    /// <summary>
    /// Resolves a MAP_INT index to a string from the name table.
    /// Returns the index as string if the table is not loaded or index is out of range.
    /// </summary>
    public string ResolveMapInt(int index)
    {
        if (index >= 0 && index < _nameTable.Length)
            return _nameTable[index];
        return index.ToString();
    }

    /// <summary>
    /// Parses ItemName records from a decrypted .dat binary buffer.
    /// Uses the GrandCrusade structure (Fafurion - Classic Secret of Empire).
    /// Fields: UINT id, MAP_INT name, ASCF additionalname, ASCF description,
    /// SHORT popup, ASCF default_action, UINT use_order, SHORT name_class,
    /// UCHAR color, MAP_INT Tooltip_Texture, UCHAR is_trade..is_commission_store.
    /// </summary>
    public List<DatItemName> ParseItemName(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatItemName>(count);

        for (int i = 0; i < count; i++)
        {
            var id = reader.ReadUInt();
            var nameIdx = reader.ReadMapInt();
            var name = ResolveMapInt(nameIdx);
            var additionalName = reader.ReadAscfString();
            var description = reader.ReadAscfString();
            var popup = reader.ReadShort();
            var defaultAction = reader.ReadAscfString();
            var useOrder = reader.ReadUInt();
            var nameClass = reader.ReadShort();
            var color = reader.ReadUByte();
            var tooltipIdx = reader.ReadMapInt();
            var tooltipTexture = ResolveMapInt(tooltipIdx);
            var isTrade = reader.ReadUByte();
            var isDrop = reader.ReadUByte();
            var isDestruct = reader.ReadUByte();
            var isPrivateStore = reader.ReadUByte();
            var keepType = reader.ReadUByte();
            var isNpcTrade = reader.ReadUByte();
            var isCommissionStore = reader.ReadUByte();

            items.Add(new DatItemName
            {
                Id = id,
                Name = name,
                AdditionalName = additionalName,
                Description = description,
                Popup = popup,
                DefaultAction = defaultAction,
                UseOrder = useOrder,
                NameClass = nameClass,
                Color = color,
                TooltipTexture = tooltipTexture,
                IsTrade = isTrade,
                IsDrop = isDrop,
                IsDestruct = isDestruct,
                IsPrivateStore = isPrivateStore,
                KeepType = keepType,
                IsNpcTrade = isNpcTrade,
                IsCommissionStore = isCommissionStore
            });
        }

        return items;
    }

    /// <summary>
    /// Parses ItemStatData records from a decrypted .dat binary buffer.
    /// Uses the Helios structure (Fafurion - Classic Secret of Empire).
    /// </summary>
    public static List<DatItemStat> ParseItemStatData(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatItemStat>(count);

        for (int i = 0; i < count; i++)
        {
            items.Add(new DatItemStat
            {
                ObjectId = reader.ReadUInt(),
                PDefense = reader.ReadUShort(),
                MDefense = reader.ReadUShort(),
                PAttack = reader.ReadUShort(),
                MAttack = reader.ReadUShort(),
                PAttackSpeed = reader.ReadUShort(),
                PHit = reader.ReadFloat(),
                MHit = reader.ReadFloat(),
                PCritical = reader.ReadFloat(),
                MCritical = reader.ReadFloat(),
                Speed = reader.ReadUByte(),
                ShieldDefense = reader.ReadUShort(),
                ShieldDefenseRate = reader.ReadUByte(),
                PAvoid = reader.ReadFloat(),
                MAvoid = reader.ReadFloat(),
                PropertyParams = reader.ReadUShort()
            });
        }

        return items;
    }

    /// <summary>
    /// Converts a list of DatItemStat records to the standard text format.
    /// </summary>
    public static string ToTextFormat(List<DatItemStat> items)
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            sb.Append("item_begin");
            sb.Append($"\tobject_id={item.ObjectId}");
            sb.Append($"\tpDefense={item.PDefense}");
            sb.Append($"\tmDefense={item.MDefense}");
            sb.Append($"\tpAttack={item.PAttack}");
            sb.Append($"\tmAttack={item.MAttack}");
            sb.Append($"\tpAttackSpeed={item.PAttackSpeed}");
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tpHit={item.PHit:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tmHit={item.MHit:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tpCritical={item.PCritical:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tmCritical={item.MCritical:0.0###}"));
            sb.Append($"\tspeed={item.Speed}");
            sb.Append($"\tShieldDefense={item.ShieldDefense}");
            sb.Append($"\tShieldDefenseRate={item.ShieldDefenseRate}");
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tpavoid={item.PAvoid:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tmavoid={item.MAvoid:0.0###}"));
            sb.Append($"\tproperty_params={item.PropertyParams}");
            sb.Append("\titem_end");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Parses L2GameDataName table from a decrypted .dat binary buffer.
    /// Structure: UINT count, then count UNICODE strings.
    /// </summary>
    public static string[] ParseGameDataName(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var names = new string[count];

        for (int i = 0; i < count; i++)
        {
            names[i] = reader.ReadUnicodeString();
        }

        return names;
    }

    /// <summary>
    /// Converts the L2GameDataName string table to the standard text format.
    /// </summary>
    public static string ToTextFormat(string[] nameTable)
    {
        var sb = new StringBuilder();

        foreach (var name in nameTable)
        {
            sb.Append("name_begin");
            sb.Append($"\tname=[{name}]");
            sb.Append("\tname_end");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Decrypts and loads the L2GameDataName table from a .dat file.
    /// </summary>
    public static string[] LoadNameTable(string gameDataNameDatPath)
    {
        var decrypted = DatCrypto.DecryptFile(gameDataNameDatPath);
        return ParseGameDataName(decrypted);
    }

    /// <summary>
    /// Parses Skillgrp records from a decrypted .dat binary buffer.
    /// Uses the Salvation structure (Fafurion - Classic Secret of Empire).
    /// </summary>
    public List<DatSkillGrp> ParseSkillGrp(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatSkillGrp>(count);

        for (int i = 0; i < count; i++)
        {
            var skillId = reader.ReadUShort();
            var skillLevel = reader.ReadUByte();
            var skillSublevel = reader.ReadShort();
            var iconType = reader.ReadUByte();
            var magicType = reader.ReadUByte();
            var operateType = reader.ReadUByte();
            var mpConsume = reader.ReadShort();
            var castRange = reader.ReadInt();
            var castStyle = reader.ReadUByte();
            var hitTime = reader.ReadFloat();
            var coolTime = reader.ReadFloat();
            var reuseDelay = reader.ReadFloat();
            var effectPoint = reader.ReadInt();
            var isMagic = reader.ReadUByte();
            var originSkill = reader.ReadShort();
            var isDouble = reader.ReadUByte();

            var animCount = (int)reader.ReadUInt();
            var animations = new string[animCount];
            for (int j = 0; j < animCount; j++)
                animations[j] = ResolveMapInt(reader.ReadMapInt());

            var skillVisualEffect = ResolveMapInt(reader.ReadMapInt());
            var icon = ResolveMapInt(reader.ReadMapInt());
            var iconPanel = ResolveMapInt(reader.ReadMapInt());
            var debuff = reader.ReadUByte();
            var resistCast = reader.ReadUByte();
            var enchantSkillLevel = reader.ReadUByte();
            var enchantIcon = ResolveMapInt(reader.ReadMapInt());
            var hpConsume = reader.ReadShort();
            var rumbleSelf = (sbyte)reader.ReadUByte();
            var rumbleTarget = (sbyte)reader.ReadUByte();

            items.Add(new DatSkillGrp
            {
                SkillId = skillId,
                SkillLevel = skillLevel,
                SkillSublevel = skillSublevel,
                IconType = iconType,
                MagicType = magicType,
                OperateType = operateType,
                MpConsume = mpConsume,
                CastRange = castRange,
                CastStyle = castStyle,
                HitTime = hitTime,
                CoolTime = coolTime,
                ReuseDelay = reuseDelay,
                EffectPoint = effectPoint,
                IsMagic = isMagic,
                OriginSkill = originSkill,
                IsDouble = isDouble,
                Animations = animations,
                SkillVisualEffect = skillVisualEffect,
                Icon = icon,
                IconPanel = iconPanel,
                Debuff = debuff,
                ResistCast = resistCast,
                EnchantSkillLevel = enchantSkillLevel,
                EnchantIcon = enchantIcon,
                HpConsume = hpConsume,
                RumbleSelf = rumbleSelf,
                RumbleTarget = rumbleTarget
            });
        }

        return items;
    }

    /// <summary>
    /// Converts a list of DatSkillGrp records to the standard text format.
    /// </summary>
    public static string ToTextFormat(List<DatSkillGrp> items)
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            sb.Append("skill_begin");
            sb.Append($"\tskill_id={item.SkillId}");
            sb.Append($"\tskill_level={item.SkillLevel}");
            sb.Append($"\tskill_sublevel={item.SkillSublevel}");
            sb.Append($"\ticon_type={item.IconType}");
            sb.Append($"\tMagicType={item.MagicType}");
            sb.Append($"\toperate_type={item.OperateType}");
            sb.Append($"\tmp_consume={item.MpConsume}");
            sb.Append($"\tcast_range={item.CastRange}");
            sb.Append($"\tcast_style={item.CastStyle}");
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\thit_time={item.HitTime:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tcool_time={item.CoolTime:0.0###}"));
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\treuse_delay={item.ReuseDelay:0.0###}"));
            sb.Append($"\teffect_point={item.EffectPoint}");
            sb.Append($"\tis_magic={item.IsMagic}");
            sb.Append($"\torigin_skill={item.OriginSkill}");
            sb.Append($"\tis_double={item.IsDouble}");
            sb.Append($"\tanimation={{{string.Join(";", item.Animations.Select(a => $"[{a}]"))}}}");
            sb.Append($"\tskill_visual_effect=[{item.SkillVisualEffect}]");
            sb.Append($"\ticon=[{item.Icon}]");
            sb.Append($"\ticon_panel=[{item.IconPanel}]");
            sb.Append($"\tdebuff={item.Debuff}");
            sb.Append($"\tresist_cast={item.ResistCast}");
            sb.Append($"\tenchant_skill_level={item.EnchantSkillLevel}");
            sb.Append($"\tenchant_icon=[{item.EnchantIcon}]");
            sb.Append($"\thp_consume={item.HpConsume}");
            sb.Append($"\trumble_self={item.RumbleSelf}");
            sb.Append($"\trumble_target={item.RumbleTarget}");
            sb.Append("\tskill_end");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Converts a list of DatItemName records to the standard text format.
    /// </summary>
    public static string ToTextFormat(List<DatItemName> items)
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            sb.Append("item_name_begin");
            sb.Append($"\tid={item.Id}");
            sb.Append($"\tname=[{item.Name}]");
            sb.Append($"\tadditionalname=[{item.AdditionalName}]");
            sb.Append($"\tdescription=[{item.Description}]");
            sb.Append($"\tpopup={item.Popup}");
            sb.Append($"\tdefault_action=[{item.DefaultAction}]");
            sb.Append($"\tuse_order={item.UseOrder}");
            sb.Append($"\tname_class={item.NameClass}");
            sb.Append($"\tcolor={item.Color}");
            sb.Append($"\tTooltip_Texture=[{item.TooltipTexture}]");
            sb.Append($"\tis_trade={item.IsTrade}");
            sb.Append($"\tis_drop={item.IsDrop}");
            sb.Append($"\tis_destruct={item.IsDestruct}");
            sb.Append($"\tis_private_store={item.IsPrivateStore}");
            sb.Append($"\tkeep_type={item.KeepType}");
            sb.Append($"\tis_npctrade={item.IsNpcTrade}");
            sb.Append($"\tis_commission_store={item.IsCommissionStore}");
            sb.Append("\titem_name_end");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    // ── Helper readers for composite structures ──────────────────────────

    private DropMeshData[] ReadDropMeshes(L2BinaryReader reader)
    {
        var count = reader.ReadUByte();
        var meshes = new DropMeshData[count];
        for (int i = 0; i < count; i++)
        {
            var mesh = ResolveMapInt(reader.ReadMapInt());
            var texCount = reader.ReadUByte();
            var textures = new string[texCount];
            for (int j = 0; j < texCount; j++)
                textures[j] = ResolveMapInt(reader.ReadMapInt());
            meshes[i] = new DropMeshData { Mesh = mesh, Textures = textures };
        }
        return meshes;
    }

    private MtxNew2 ReadMtxNew2(L2BinaryReader reader)
    {
        var meshCount = reader.ReadUByte();
        var meshes = new string[meshCount];
        for (int i = 0; i < meshCount; i++)
            meshes[i] = ResolveMapInt(reader.ReadMapInt());
        var texCount = reader.ReadUByte();
        var textures = new string[texCount];
        for (int i = 0; i < texCount; i++)
            textures[i] = ResolveMapInt(reader.ReadMapInt());
        return new MtxNew2 { Meshes = meshes, Textures = textures };
    }

    private Mtx3New2 ReadMtx3New2(L2BinaryReader reader)
    {
        var meshCount = reader.ReadUByte();
        var meshes = new string[meshCount];
        for (int i = 0; i < meshCount; i++)
            meshes[i] = ResolveMapInt(reader.ReadMapInt());
        var meshParams = new (int, sbyte)[meshCount];
        for (int i = 0; i < meshCount; i++)
            meshParams[i] = (reader.ReadUByte(), (sbyte)reader.ReadUByte());
        var texCount = reader.ReadUByte();
        var textures = new string[texCount];
        for (int i = 0; i < texCount; i++)
            textures[i] = ResolveMapInt(reader.ReadMapInt());
        var texExt = ResolveMapInt(reader.ReadMapInt());
        return new Mtx3New2 { Meshes = meshes, MeshParams = meshParams, Textures = textures, TextExt = texExt };
    }

    private string[] ReadMapIntArray(L2BinaryReader reader, int count)
    {
        var arr = new string[count];
        for (int i = 0; i < count; i++)
            arr[i] = ResolveMapInt(reader.ReadMapInt());
        return arr;
    }

    // ── SkillName Parser (EtinasFate) ────────────────────────────────────

    /// <summary>
    /// Parses SkillName records from a decrypted .dat binary buffer.
    /// Uses the EtinasFate structure (Fafurion - Classic Secret of Empire).
    /// Has an indexed string table followed by skill records.
    /// </summary>
    public static List<DatSkillName> ParseSkillName(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);

        // Read string table
        var strCount = reader.ReadCompactInt();
        var stringTable = new Dictionary<uint, string>(strCount);
        for (int i = 0; i < strCount; i++)
        {
            var text = reader.ReadAscfString();
            var index = reader.ReadUInt();
            stringTable[index] = text;
        }

        string Resolve(uint idx) => stringTable.TryGetValue(idx, out var s) ? s : string.Empty;

        // Read skill records
        var skillCount = (int)reader.ReadUInt();
        var skills = new List<DatSkillName>(skillCount);

        for (int i = 0; i < skillCount; i++)
        {
            var skillId = reader.ReadUShort();
            var skillLevel = reader.ReadUByte();
            var skillSublevel = reader.ReadUShort();
            var name = Resolve(reader.ReadUInt());
            var desc = Resolve(reader.ReadUInt());
            var descParam = Resolve(reader.ReadUInt());
            var enchantName = Resolve(reader.ReadUInt());
            var enchantNameParam = Resolve(reader.ReadUInt());
            var enchantDesc = Resolve(reader.ReadUInt());
            var enchantDescParam = Resolve(reader.ReadUInt());

            skills.Add(new DatSkillName
            {
                SkillId = skillId,
                SkillLevel = skillLevel,
                SkillSublevel = skillSublevel,
                Name = name,
                Desc = desc,
                DescParam = descParam,
                EnchantName = enchantName,
                EnchantNameParam = enchantNameParam,
                EnchantDesc = enchantDesc,
                EnchantDescParam = enchantDescParam
            });
        }

        return skills;
    }

    public static string ToTextFormat(List<DatSkillName> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("skill_begin");
            sb.Append($"\tskill_id={item.SkillId}");
            sb.Append($"\tskill_level={item.SkillLevel}");
            sb.Append($"\tskill_sublevel={item.SkillSublevel}");
            sb.Append($"\tname=[{item.Name}]");
            sb.Append($"\tdesc=[{item.Desc}]");
            sb.Append($"\tdesc_param=[{item.DescParam}]");
            sb.Append($"\tenchant_name=[{item.EnchantName}]");
            sb.Append($"\tenchant_name_param=[{item.EnchantNameParam}]");
            sb.Append($"\tenchant_desc=[{item.EnchantDesc}]");
            sb.Append($"\tenchant_desc_param=[{item.EnchantDescParam}]");
            sb.Append("\tskill_end");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ── SystemMsg Binary Serializer ──────────────────────────────────────

    /// <summary>
    /// Serializes a list of DatSystemMsg records back to the Helios binary format.
    /// Mirrors ParseSystemMsg exactly — must be kept in sync with the parser.
    /// </summary>
    public static byte[] SerializeSystemMsg(List<DatSystemMsg> items)
    {
        using var ms = new MemoryStream();

        WriteUInt32(ms, (uint)items.Count);

        foreach (var item in items)
        {
            WriteUInt32(ms, item.Id);
            WriteUInt32(ms, item.Unk0);
            WriteAscf(ms, item.Message);
            WriteUInt32(ms, item.Group);

            // RGBA: 4 bytes from AARRGGBB hex string
            var c = item.Color.PadLeft(8, '0');
            ms.WriteByte(Convert.ToByte(c[0..2], 16)); // A
            ms.WriteByte(Convert.ToByte(c[2..4], 16)); // R
            ms.WriteByte(Convert.ToByte(c[4..6], 16)); // G
            ms.WriteByte(Convert.ToByte(c[6..8], 16)); // B

            WriteInt32(ms, item.SoundIndex); // MAP_INT raw index
            WriteInt32(ms, item.VoiceIndex); // MAP_INT raw index
            WriteUInt32(ms, item.Win);
            WriteUInt32(ms, item.Font);
            WriteUInt32(ms, item.LfTime);
            WriteUInt32(ms, item.Bkg);
            WriteUInt32(ms, item.Anim);
            WriteAscf(ms, item.ScrnMsg);
            WriteAscf(ms, item.GfxScrnMsg);
            WriteAscf(ms, item.GfxScrnParam);
            WriteAscf(ms, item.Type);
        }

        // SafePackage footer — required for isSafePackage="true" .dat files.
        // Equivalent to Java's DescriptorWriter.END_FILE_BYTES:
        // ASCF string "SafePackage" = compact-int 12 + "SafePackage\0" (11 chars + null)
        ms.Write(new byte[] { 12, 83, 97, 102, 101, 80, 97, 99, 107, 97, 103, 101, 0 });

        return ms.ToArray();
    }

    private static void WriteUInt32(MemoryStream ms, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        ms.Write(buf);
    }

    private static void WriteInt32(MemoryStream ms, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        ms.Write(buf);
    }

    /// <summary>
    /// Writes an ASCF string: compact-int length prefix + encoded bytes + null terminator.
    /// Positive length = Latin1 (cp1252). Negative length = UTF-16LE.
    /// </summary>
    private static void WriteAscf(MemoryStream ms, string s)
    {
        if (s.Length == 0)
        {
            WriteCompactInt(ms, 0);
            return;
        }

        if (s.All(c => c <= 0xFF))
        {
            // Latin1 path: length includes null terminator
            var bytes = Encoding.Latin1.GetBytes(s);
            WriteCompactInt(ms, bytes.Length + 1);
            ms.Write(bytes);
            ms.WriteByte(0);
        }
        else
        {
            // UTF-16LE path: negative length = char count + 1 null char
            var bytes = Encoding.Unicode.GetBytes(s);
            WriteCompactInt(ms, -(s.Length + 1));
            ms.Write(bytes);
            ms.WriteByte(0); ms.WriteByte(0);
        }
    }

    /// <summary>
    /// Writes a compact variable-length integer (inverse of ReadCompactInt).
    /// Byte 0: bit7=sign, bit6=continuation, bits5-0=value[5:0].
    /// Subsequent bytes: bit7=continuation, bits6-0=value[N:N-6].
    /// </summary>
    private static void WriteCompactInt(MemoryStream ms, int value)
    {
        bool negative = value < 0;
        int abs = Math.Abs(value);

        if (abs < 64)
        {
            ms.WriteByte((byte)((abs & 0x3F) | (negative ? 0x80 : 0)));
            return;
        }

        ms.WriteByte((byte)((abs & 0x3F) | 0x40 | (negative ? 0x80 : 0)));
        abs >>= 6;

        while (abs >= 128)
        {
            ms.WriteByte((byte)((abs & 0x7F) | 0x80));
            abs >>= 7;
        }
        ms.WriteByte((byte)(abs & 0x7F));
    }

    // ── EtcItemgrp Parser (Helios) ───────────────────────────────────────

    /// <summary>
    /// Parses EtcItemgrp records from a decrypted .dat binary buffer.
    /// Uses the Helios structure (Fafurion - Classic Secret of Empire).
    /// </summary>
    public List<DatEtcItemGrp> ParseEtcItemGrp(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatEtcItemGrp>(count);

        for (int i = 0; i < count; i++)
        {
            var tag = reader.ReadUByte();
            var objectId = reader.ReadUInt();
            var dropType = reader.ReadUByte();
            var dropAnimType = reader.ReadUByte();
            var dropRadius = reader.ReadUByte();
            var dropHeight = reader.ReadUByte();

            var dropMeshes = ReadDropMeshes(reader);

            var icon1 = ResolveMapInt(reader.ReadMapInt());
            var icon2 = ResolveMapInt(reader.ReadMapInt());
            var icon3 = ResolveMapInt(reader.ReadMapInt());
            var icon4 = ResolveMapInt(reader.ReadMapInt());
            var icon5 = ResolveMapInt(reader.ReadMapInt());

            var durability = reader.ReadShort();
            var weight = reader.ReadShort();
            var materialType = reader.ReadUByte();
            var crystallizable = reader.ReadUByte();

            var questCount = reader.ReadUByte();
            var questIds = new short[questCount];
            for (int j = 0; j < questCount; j++)
                questIds[j] = reader.ReadShort();

            var color = reader.ReadUByte();
            var isAttribution = reader.ReadUByte();
            var propertyParams = reader.ReadShort();
            var iconPanel = ResolveMapInt(reader.ReadMapInt());
            var completeDropSound = ResolveMapInt(reader.ReadMapInt());
            var inventoryType = reader.ReadUByte();

            // MTX_NEW2 for mesh/texture
            var meshCount = reader.ReadUByte();
            var meshes = ReadMapIntArray(reader, meshCount);
            var texCount = reader.ReadUByte();
            var textures = ReadMapIntArray(reader, texCount);

            var dropSound = ResolveMapInt(reader.ReadMapInt());
            var equipSound = ResolveMapInt(reader.ReadMapInt());
            var consumeType = reader.ReadUByte();
            var etcItemType = reader.ReadUInt();
            var crystalType = reader.ReadUByte();

            items.Add(new DatEtcItemGrp
            {
                Tag = tag,
                ObjectId = objectId,
                DropType = dropType,
                DropAnimType = dropAnimType,
                DropRadius = dropRadius,
                DropHeight = dropHeight,
                DropMeshes = dropMeshes,
                Icon1 = icon1,
                Icon2 = icon2,
                Icon3 = icon3,
                Icon4 = icon4,
                Icon5 = icon5,
                Durability = durability,
                Weight = weight,
                MaterialType = materialType,
                Crystallizable = crystallizable,
                RelatedQuestIds = questIds,
                Color = color,
                IsAttribution = isAttribution,
                PropertyParams = propertyParams,
                IconPanel = iconPanel,
                CompleteItemDropSoundType = completeDropSound,
                InventoryType = inventoryType,
                Meshes = meshes,
                Textures = textures,
                DropSound = dropSound,
                EquipSound = equipSound,
                ConsumeType = consumeType,
                EtcItemType = etcItemType,
                CrystalType = crystalType
            });
        }

        return items;
    }

    public static string ToTextFormat(List<DatEtcItemGrp> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("item_begin");
            sb.Append($"\ttag={item.Tag}");
            sb.Append($"\tobject_id={item.ObjectId}");
            sb.Append($"\tdrop_type={item.DropType}");
            sb.Append($"\tdrop_anim_type={item.DropAnimType}");
            sb.Append($"\tdrop_radius={item.DropRadius}");
            sb.Append($"\tdrop_height={item.DropHeight}");
            AppendDropTexture(sb, item.DropMeshes);
            AppendIconCompound(sb, item.Icon1, item.Icon2, item.Icon3, item.Icon4, item.Icon5);
            sb.Append($"\tdurability={item.Durability}");
            sb.Append($"\tweight={item.Weight}");
            sb.Append($"\tmaterial_type={DatEnums.Resolve(DatEnums.MaterialType, item.MaterialType)}");
            sb.Append($"\tcrystallizable={item.Crystallizable}");
            sb.Append($"\trelated_quest_id={{{string.Join(";", item.RelatedQuestIds)}}}");
            sb.Append($"\tcolor={item.Color}");
            sb.Append($"\tis_attribution={item.IsAttribution}");
            sb.Append($"\tproperty_params={item.PropertyParams}");
            sb.Append($"\ticon_panel=[{item.IconPanel}]");
            sb.Append($"\tcomplete_item_dropsound_type=[{item.CompleteItemDropSoundType}]");
            sb.Append($"\tinventory_type={DatEnums.Resolve(DatEnums.InventoryType, item.InventoryType)}");
            AppendBracketedArray(sb, "mesh", item.Meshes);
            AppendBracketedArray(sb, "texture", item.Textures);
            sb.Append($"\tdrop_sound=[{item.DropSound}]");
            sb.Append($"\tequip_sound=[{item.EquipSound}]");
            sb.Append($"\tconsume_type={DatEnums.Resolve(DatEnums.ConsumeType, item.ConsumeType)}");
            sb.Append($"\tetcitem_type={DatEnums.Resolve(DatEnums.EtcItemType, (int)item.EtcItemType)}");
            sb.Append($"\tcrystal_type={DatEnums.Resolve(DatEnums.CrystalType, item.CrystalType)}");
            sb.Append("\titem_end");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ── Armorgrp Parser (Helios) ─────────────────────────────────────────

    /// <summary>
    /// Parses Armorgrp records from a decrypted .dat binary buffer.
    /// Uses the Helios structure (Fafurion - Classic Secret of Empire).
    /// </summary>
    public List<DatArmorGrp> ParseArmorGrp(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatArmorGrp>(count);

        for (int i = 0; i < count; i++)
        {
            var tag = reader.ReadUByte();
            var objectId = reader.ReadUInt();
            var dropType = reader.ReadUByte();
            var dropAnimType = reader.ReadUByte();
            var dropRadius = reader.ReadUByte();
            var dropHeight = reader.ReadUByte();

            var dropMeshes = ReadDropMeshes(reader);

            var icon1 = ResolveMapInt(reader.ReadMapInt());
            var icon2 = ResolveMapInt(reader.ReadMapInt());
            var icon3 = ResolveMapInt(reader.ReadMapInt());
            var icon4 = ResolveMapInt(reader.ReadMapInt());
            var icon5 = ResolveMapInt(reader.ReadMapInt());

            var durability = reader.ReadShort();
            var weight = reader.ReadShort();
            var materialType = reader.ReadUByte();
            var crystallizable = reader.ReadUByte();

            var questCount = reader.ReadUByte();
            var questIds = new short[questCount];
            for (int j = 0; j < questCount; j++)
                questIds[j] = reader.ReadShort();

            var color = reader.ReadUByte();
            var isAttribution = reader.ReadUByte();
            var propertyParams = reader.ReadShort();
            var iconPanel = ResolveMapInt(reader.ReadMapInt());
            var completeDropSound = ResolveMapInt(reader.ReadMapInt());
            var inventoryType = reader.ReadUByte();
            var bodyPart = reader.ReadUByte();

            // 19 race slots: MTX_NEW2 + MTX3_NEW2 for each
            const int raceSlots = 19;
            var raceMeshes = new MtxNew2[raceSlots];
            var raceMeshesAdd = new Mtx3New2[raceSlots];
            for (int r = 0; r < raceSlots; r++)
            {
                raceMeshes[r] = ReadMtxNew2(reader);
                raceMeshesAdd[r] = ReadMtx3New2(reader);
            }

            var attackEffect = ResolveMapInt(reader.ReadMapInt());

            var soundCount = reader.ReadUByte();
            var itemSounds = ReadMapIntArray(reader, soundCount);

            var dropSound = ResolveMapInt(reader.ReadMapInt());
            var equipSound = ResolveMapInt(reader.ReadMapInt());
            var unk7 = reader.ReadUInt();
            var unk6 = reader.ReadUByte();
            var armorType = reader.ReadUByte();
            var crystalType = reader.ReadUByte();
            var mpBonus = reader.ReadShort();
            var hideMask = reader.ReadShort();
            var underwearBodyPart1 = reader.ReadUByte();
            var underwearBodyPart2 = reader.ReadUByte();
            var fullArmorEnchantEffectType = (sbyte)reader.ReadUByte();

            items.Add(new DatArmorGrp
            {
                Tag = tag,
                ObjectId = objectId,
                DropType = dropType,
                DropAnimType = dropAnimType,
                DropRadius = dropRadius,
                DropHeight = dropHeight,
                DropMeshes = dropMeshes,
                Icon1 = icon1,
                Icon2 = icon2,
                Icon3 = icon3,
                Icon4 = icon4,
                Icon5 = icon5,
                Durability = durability,
                Weight = weight,
                MaterialType = materialType,
                Crystallizable = crystallizable,
                RelatedQuestIds = questIds,
                Color = color,
                IsAttribution = isAttribution,
                PropertyParams = propertyParams,
                IconPanel = iconPanel,
                CompleteItemDropSoundType = completeDropSound,
                InventoryType = inventoryType,
                BodyPart = bodyPart,
                RaceMeshes = raceMeshes,
                RaceMeshesAdd = raceMeshesAdd,
                AttackEffect = attackEffect,
                ItemSounds = itemSounds,
                DropSound = dropSound,
                EquipSound = equipSound,
                Unk7 = unk7,
                Unk6 = unk6,
                ArmorType = armorType,
                CrystalType = crystalType,
                MpBonus = mpBonus,
                HideMask = hideMask,
                UnderwearBodyPart1 = underwearBodyPart1,
                UnderwearBodyPart2 = underwearBodyPart2,
                FullArmorEnchantEffectType = fullArmorEnchantEffectType
            });
        }

        return items;
    }

    public static string ToTextFormat(List<DatArmorGrp> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("item_begin");
            sb.Append($"\ttag={item.Tag}");
            sb.Append($"\tobject_id={item.ObjectId}");
            sb.Append($"\tdrop_type={item.DropType}");
            sb.Append($"\tdrop_anim_type={item.DropAnimType}");
            sb.Append($"\tdrop_radius={item.DropRadius}");
            sb.Append($"\tdrop_height={item.DropHeight}");
            AppendDropTexture(sb, item.DropMeshes);
            AppendIconCompound(sb, item.Icon1, item.Icon2, item.Icon3, item.Icon4, item.Icon5);
            sb.Append($"\tdurability={item.Durability}");
            sb.Append($"\tweight={item.Weight}");
            sb.Append($"\tmaterial_type={DatEnums.Resolve(DatEnums.MaterialType, item.MaterialType)}");
            sb.Append($"\tcrystallizable={item.Crystallizable}");
            sb.Append($"\trelated_quest_id={{{string.Join(";", item.RelatedQuestIds)}}}");
            sb.Append($"\tcolor={item.Color}");
            sb.Append($"\tis_attribution={item.IsAttribution}");
            sb.Append($"\tproperty_params={item.PropertyParams}");
            sb.Append($"\ticon_panel=[{item.IconPanel}]");
            sb.Append($"\tcomplete_item_dropsound_type=[{item.CompleteItemDropSoundType}]");
            sb.Append($"\tinventory_type={DatEnums.Resolve(DatEnums.InventoryType, item.InventoryType)}");
            sb.Append($"\tbody_part={DatEnums.Resolve(DatEnums.BodyPart, item.BodyPart)}");

            for (int r = 0; r < DatArmorGrp.RaceSlotNames.Length && r < item.RaceMeshes.Length; r++)
            {
                AppendMtxNew2(sb, DatArmorGrp.RaceSlotNames[r], item.RaceMeshes[r]);
                AppendMtx3New2(sb, DatArmorGrp.RaceSlotAddNames[r], item.RaceMeshesAdd[r]);
            }

            sb.Append($"\tattack_effect=[{item.AttackEffect}]");
            AppendBracketedArray(sb, "item_sound", item.ItemSounds);
            sb.Append($"\tdrop_sound=[{item.DropSound}]");
            sb.Append($"\tequip_sound=[{item.EquipSound}]");
            sb.Append($"\tUNK_7={item.Unk7}");
            sb.Append($"\tUNK_6={item.Unk6}");
            sb.Append($"\tarmor_type={DatEnums.Resolve(DatEnums.ArmorType, item.ArmorType)}");
            sb.Append($"\tcrystal_type={DatEnums.Resolve(DatEnums.CrystalType, item.CrystalType)}");
            sb.Append($"\tmp_bonus={item.MpBonus}");
            sb.Append($"\thide_mask={item.HideMask}");
            sb.Append($"\tunderwear_body_part1={item.UnderwearBodyPart1}");
            sb.Append($"\tunderwear_body_part2={item.UnderwearBodyPart2}");
            sb.Append($"\tfull_armor_enchant_effect_type={item.FullArmorEnchantEffectType}");
            sb.Append("\titem_end");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ── Weapongrp Parser (Helios) ────────────────────────────────────────

    /// <summary>
    /// Parses Weapongrp records from a decrypted .dat binary buffer.
    /// Uses the Helios structure (Fafurion - Classic Secret of Empire).
    /// </summary>
    public List<DatWeaponGrp> ParseWeaponGrp(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatWeaponGrp>(count);

        for (int i = 0; i < count; i++)
        {
            var tag = reader.ReadUByte();
            var objectId = reader.ReadUInt();
            var dropType = reader.ReadUByte();
            var dropAnimType = reader.ReadUByte();
            var dropRadius = reader.ReadUByte();
            var dropHeight = reader.ReadUByte();

            var dropMeshes = ReadDropMeshes(reader);

            var icon1 = ResolveMapInt(reader.ReadMapInt());
            var icon2 = ResolveMapInt(reader.ReadMapInt());
            var icon3 = ResolveMapInt(reader.ReadMapInt());
            var icon4 = ResolveMapInt(reader.ReadMapInt());
            var icon5 = ResolveMapInt(reader.ReadMapInt());

            var durability = reader.ReadShort();
            var weight = reader.ReadShort();
            var materialType = reader.ReadUByte();
            var crystallizable = reader.ReadUByte();

            var questCount = reader.ReadUByte();
            var questIds = new short[questCount];
            for (int j = 0; j < questCount; j++)
                questIds[j] = reader.ReadShort();

            var color = reader.ReadUByte();
            var isAttribution = reader.ReadUByte();
            var propertyParams = reader.ReadShort();
            var iconPanel = ResolveMapInt(reader.ReadMapInt());
            var completeDropSound = ResolveMapInt(reader.ReadMapInt());
            var inventoryType = reader.ReadUByte();
            var bodyPart = reader.ReadUByte();
            var handness = reader.ReadUByte();

            // wp_mesh: UCHAR count, count×MAP_INT mesh0, count×UCHAR mesh1
            var wpMeshCount = reader.ReadUByte();
            var wpMeshes = ReadMapIntArray(reader, wpMeshCount);
            var wpMeshFlags = new int[wpMeshCount];
            for (int j = 0; j < wpMeshCount; j++)
                wpMeshFlags[j] = reader.ReadUByte();

            var texCount = reader.ReadUByte();
            var textures = ReadMapIntArray(reader, texCount);

            var soundCount = reader.ReadUByte();
            var itemSounds = ReadMapIntArray(reader, soundCount);

            var dropSound = ResolveMapInt(reader.ReadMapInt());
            var equipSound = ResolveMapInt(reader.ReadMapInt());
            var effect = ResolveMapInt(reader.ReadMapInt());
            var randomDamage = reader.ReadUByte();
            var weaponType = reader.ReadUByte();
            var crystalType = reader.ReadUByte();
            var mpConsume = reader.ReadUByte();
            var soulshotCount = reader.ReadUByte();
            var spiritshotCount = reader.ReadUByte();
            var curvature = reader.ReadShort();
            var unk10 = reader.ReadUByte();
            var canEquipHero = (sbyte)reader.ReadUByte();
            var isMagicWeapon = reader.ReadUByte();
            var ertheiaFistScale = reader.ReadFloat();
            var junk = reader.ReadShort();

            // Enchanted effects
            var enchantedCount = reader.ReadUByte();
            var enchantedEffects = new EnchantedEffectData[enchantedCount];
            for (int e = 0; e < enchantedCount; e++)
            {
                enchantedEffects[e] = new EnchantedEffectData
                {
                    Effect = ResolveMapInt(reader.ReadMapInt()),
                    OffsetX = reader.ReadFloat(),
                    OffsetY = reader.ReadFloat(),
                    OffsetZ = reader.ReadFloat(),
                    MeshOffsetX = reader.ReadFloat(),
                    MeshOffsetY = reader.ReadFloat(),
                    MeshOffsetZ = reader.ReadFloat(),
                    MeshScaleX = reader.ReadFloat(),
                    MeshScaleY = reader.ReadFloat(),
                    MeshScaleZ = reader.ReadFloat(),
                    Velocity = reader.ReadFloat(),
                    ParticleScale = reader.ReadFloat(),
                    EffectScale = reader.ReadFloat(),
                    ParticleOffsetX = reader.ReadFloat(),
                    ParticleOffsetY = reader.ReadFloat(),
                    ParticleOffsetZ = reader.ReadFloat(),
                    RingOffsetX = reader.ReadFloat(),
                    RingOffsetY = reader.ReadFloat(),
                    RingOffsetZ = reader.ReadFloat(),
                    RingScaleX = reader.ReadFloat(),
                    RingScaleY = reader.ReadFloat(),
                    RingScaleZ = reader.ReadFloat()
                };
            }

            // variation_effect_type (6 UCHARs interpreted as signed)
            var variationEffectTypes = new sbyte[6];
            for (int v = 0; v < 6; v++)
                variationEffectTypes[v] = (sbyte)reader.ReadUByte();

            var variationIconCount = reader.ReadUByte();
            var variationIcons = ReadMapIntArray(reader, variationIconCount);

            var normalEnsoulCount = reader.ReadUByte();
            var specialEnsoulCount = reader.ReadUByte();

            items.Add(new DatWeaponGrp
            {
                Tag = tag,
                ObjectId = objectId,
                DropType = dropType,
                DropAnimType = dropAnimType,
                DropRadius = dropRadius,
                DropHeight = dropHeight,
                DropMeshes = dropMeshes,
                Icon1 = icon1,
                Icon2 = icon2,
                Icon3 = icon3,
                Icon4 = icon4,
                Icon5 = icon5,
                Durability = durability,
                Weight = weight,
                MaterialType = materialType,
                Crystallizable = crystallizable,
                RelatedQuestIds = questIds,
                Color = color,
                IsAttribution = isAttribution,
                PropertyParams = propertyParams,
                IconPanel = iconPanel,
                CompleteItemDropSoundType = completeDropSound,
                InventoryType = inventoryType,
                BodyPart = bodyPart,
                Handness = handness,
                WpMeshes = wpMeshes,
                WpMeshFlags = wpMeshFlags,
                Textures = textures,
                ItemSounds = itemSounds,
                DropSound = dropSound,
                EquipSound = equipSound,
                Effect = effect,
                RandomDamage = randomDamage,
                WeaponType = weaponType,
                CrystalType = crystalType,
                MpConsume = mpConsume,
                SoulshotCount = soulshotCount,
                SpiritshotCount = spiritshotCount,
                Curvature = curvature,
                Unk10 = unk10,
                CanEquipHero = canEquipHero,
                IsMagicWeapon = isMagicWeapon,
                ErtheiaFistScale = ertheiaFistScale,
                Junk = junk,
                EnchantedEffects = enchantedEffects,
                VariationEffectTypes = variationEffectTypes,
                VariationIcons = variationIcons,
                NormalEnsoulCount = normalEnsoulCount,
                SpecialEnsoulCount = specialEnsoulCount
            });
        }

        return items;
    }

    public static string ToTextFormat(List<DatWeaponGrp> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("item_begin");
            sb.Append($"\ttag={item.Tag}");
            sb.Append($"\tobject_id={item.ObjectId}");
            sb.Append($"\tdrop_type={item.DropType}");
            sb.Append($"\tdrop_anim_type={item.DropAnimType}");
            sb.Append($"\tdrop_radius={item.DropRadius}");
            sb.Append($"\tdrop_height={item.DropHeight}");
            AppendDropTexture(sb, item.DropMeshes);
            AppendIconCompound(sb, item.Icon1, item.Icon2, item.Icon3, item.Icon4, item.Icon5);
            sb.Append($"\tdurability={item.Durability}");
            sb.Append($"\tweight={item.Weight}");
            sb.Append($"\tmaterial_type={DatEnums.Resolve(DatEnums.MaterialType, item.MaterialType)}");
            sb.Append($"\tcrystallizable={item.Crystallizable}");
            sb.Append($"\trelated_quest_id={{{string.Join(";", item.RelatedQuestIds)}}}");
            sb.Append($"\tcolor={item.Color}");
            sb.Append($"\tis_attribution={item.IsAttribution}");
            sb.Append($"\tproperty_params={item.PropertyParams}");
            sb.Append($"\ticon_panel=[{item.IconPanel}]");
            sb.Append($"\tcomplete_item_dropsound_type=[{item.CompleteItemDropSoundType}]");
            sb.Append($"\tinventory_type={DatEnums.Resolve(DatEnums.InventoryType, item.InventoryType)}");
            sb.Append($"\tbody_part={DatEnums.Resolve(DatEnums.BodyPart, item.BodyPart)}");
            sb.Append($"\thandness={item.Handness}");
            AppendWpMesh(sb, item.WpMeshes, item.WpMeshFlags);
            AppendBracketedArray(sb, "texture", item.Textures);
            AppendBracketedArray(sb, "item_sound", item.ItemSounds);
            sb.Append($"\tdrop_sound=[{item.DropSound}]");
            sb.Append($"\tequip_sound=[{item.EquipSound}]");
            sb.Append($"\teffect=[{item.Effect}]");
            sb.Append($"\trandom_damage={item.RandomDamage}");
            sb.Append($"\tweapon_type={DatEnums.Resolve(DatEnums.WeaponType, item.WeaponType)}");
            sb.Append($"\tcrystal_type={DatEnums.Resolve(DatEnums.CrystalType, item.CrystalType)}");
            sb.Append($"\tmp_consume={item.MpConsume}");
            sb.Append($"\tsoulshot_count={item.SoulshotCount}");
            sb.Append($"\tspiritshot_count={item.SpiritshotCount}");
            sb.Append($"\tcurvature={item.Curvature}");
            sb.Append($"\tUNK_10={item.Unk10}");
            sb.Append($"\tcan_equip_hero={item.CanEquipHero}");
            sb.Append($"\tis_magic_weapon={item.IsMagicWeapon}");
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"\tertheia_fist_scale={item.ErtheiaFistScale:0.0###}"));
            sb.Append($"\tjunk={item.Junk}");
            AppendEnchantedCompound(sb, item.EnchantedEffects);
            sb.Append($"\tvariation_effect_type={{{string.Join(";", item.VariationEffectTypes)}}}");
            AppendBracketedArray(sb, "variation_icon", item.VariationIcons);
            sb.Append($"\tensoul_slot_count={{{item.NormalEnsoulCount};{item.SpecialEnsoulCount}}}");
            sb.Append("\titem_end");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ── SystemMsg Parser (Helios) ────────────────────────────────────────

    /// <summary>
    /// Parses SystemMsg records from a decrypted .dat binary buffer.
    /// Uses the Helios structure (Fafurion - Classic Secret of Empire).
    /// Fields: UINT id, UINT UNK_0, ASCF message, UINT group, RGBA color,
    /// MAP_INT sound, MAP_INT voice, UINT win, UINT font, UINT lftime,
    /// UINT bkg, UINT anim, ASCF scrnmsg, ASCF gfxscrnmsg, ASCF gfxscrnparam, ASCF type.
    /// </summary>
    public List<DatSystemMsg> ParseSystemMsg(byte[] decryptedData)
    {
        var reader = new L2BinaryReader(decryptedData);
        var count = (int)reader.ReadUInt();
        var items = new List<DatSystemMsg>(count);

        for (int i = 0; i < count; i++)
        {
            var id           = reader.ReadUInt();
            var unk0         = reader.ReadUInt();
            var message      = reader.ReadAscfString();
            var group        = reader.ReadUInt();
            var color        = reader.ReadRgba();
            var soundIdx     = reader.ReadMapInt();
            var voiceIdx     = reader.ReadMapInt();
            var win          = reader.ReadUInt();
            var font         = reader.ReadUInt();
            var lfTime       = reader.ReadUInt();
            var bkg          = reader.ReadUInt();
            var anim         = reader.ReadUInt();
            var scrnMsg      = reader.ReadAscfString();
            var gfxScrnMsg   = reader.ReadAscfString();
            var gfxScrnParam = reader.ReadAscfString();
            var type         = reader.ReadAscfString();

            items.Add(new DatSystemMsg
            {
                Id           = id,
                Unk0         = unk0,
                Message      = message,
                Group        = group,
                Color        = color,
                SoundIndex   = soundIdx,
                Sound        = ResolveMapInt(soundIdx),
                VoiceIndex   = voiceIdx,
                Voice        = ResolveMapInt(voiceIdx),
                Win          = win,
                Font         = font,
                LfTime       = lfTime,
                Bkg          = bkg,
                Anim         = anim,
                ScrnMsg      = scrnMsg,
                GfxScrnMsg   = gfxScrnMsg,
                GfxScrnParam = gfxScrnParam,
                Type         = type
            });
        }

        return items;
    }

    public static string ToTextFormat(List<DatSystemMsg> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append("msg_begin");
            sb.Append($"\tid={item.Id}");
            sb.Append($"\tUNK_0={item.Unk0}");
            sb.Append($"\tmessage=[{item.Message}]");
            sb.Append($"\tgroup={item.Group}");
            sb.Append($"\tcolor={item.Color}");
            sb.Append($"\tsound=[{item.Sound}]");
            sb.Append($"\tvoice=[{item.Voice}]");
            sb.Append($"\twin={item.Win}");
            sb.Append($"\tfont={item.Font}");
            sb.Append($"\tlftime={item.LfTime}");
            sb.Append($"\tbkg={item.Bkg}");
            sb.Append($"\tanim={item.Anim}");
            sb.Append($"\tscrnmsg=[{item.ScrnMsg}]");
            sb.Append($"\tgfxscrnmsg=[{item.GfxScrnMsg}]");
            sb.Append($"\tgfxscrnparam=[{item.GfxScrnParam}]");
            sb.Append($"\ttype=[{item.Type}]");
            sb.Append("\tmsg_end");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ── Shared formatting helpers ────────────────────────────────────────

    /// <summary>Format: drop_texture={{[mesh1];{[tex1];[tex2]}};{[mesh2];{[tex3]}}}</summary>
    private static void AppendDropTexture(StringBuilder sb, DropMeshData[] meshes)
    {
        sb.Append("\tdrop_texture={");
        for (int i = 0; i < meshes.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"{{[{meshes[i].Mesh}];{{");
            for (int j = 0; j < meshes[i].Textures.Length; j++)
            {
                if (j > 0) sb.Append(';');
                sb.Append($"[{meshes[i].Textures[j]}]");
            }
            sb.Append("}}");
        }
        sb.Append('}');
    }

    /// <summary>Format: icon={[i1];[i2];[i3];[i4];[i5]}</summary>
    private static void AppendIconCompound(StringBuilder sb, string i1, string i2, string i3, string i4, string i5)
    {
        sb.Append($"\ticon={{[{i1}];[{i2}];[{i3}];[{i4}];[{i5}]}}");
    }

    /// <summary>Format: field={[val1];[val2];...}</summary>
    private static void AppendBracketedArray(StringBuilder sb, string name, string[] values)
    {
        sb.Append($"\t{name}={{");
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{values[i]}]");
        }
        sb.Append('}');
    }

    /// <summary>Format: name={{[mesh1];[mesh2]};{[tex1];[tex2]}}</summary>
    private static void AppendMtxNew2(StringBuilder sb, string name, MtxNew2 mtx)
    {
        sb.Append($"\t{name}={{{{");
        for (int i = 0; i < mtx.Meshes.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{mtx.Meshes[i]}]");
        }
        sb.Append("};{");
        for (int i = 0; i < mtx.Textures.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{mtx.Textures[i]}]");
        }
        sb.Append("}}");
    }

    /// <summary>Format: name={{{[mesh1];[mesh2]};{{v1;v2};{v3;v4}}};{[tex1];[tex2]};[texext]}</summary>
    private static void AppendMtx3New2(StringBuilder sb, string name, Mtx3New2 mtx)
    {
        sb.Append($"\t{name}={{{{{{");
        for (int i = 0; i < mtx.Meshes.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{mtx.Meshes[i]}]");
        }
        sb.Append("};{");
        for (int i = 0; i < mtx.MeshParams.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"{{{mtx.MeshParams[i].Val1};{mtx.MeshParams[i].Val2}}}");
        }
        sb.Append("}};{");
        for (int i = 0; i < mtx.Textures.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{mtx.Textures[i]}]");
        }
        sb.Append($"}};[{mtx.TextExt}]}}");
    }

    /// <summary>Format: wp_mesh={{[mesh1];[mesh2]};{flag1;flag2}}</summary>
    private static void AppendWpMesh(StringBuilder sb, string[] meshes, int[] flags)
    {
        sb.Append("\twp_mesh={{");
        for (int i = 0; i < meshes.Length; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append($"[{meshes[i]}]");
        }
        sb.Append("};{");
        sb.Append(string.Join(";", flags));
        sb.Append("}}");
    }

    /// <summary>Format: Enchanted={{[eff];{x;y;z};{x;y;z};{x;y;z};v;ps;es;{x;y;z};{x;y;z};{x;y;z}};...}</summary>
    private static void AppendEnchantedCompound(StringBuilder sb, EnchantedEffectData[] effects)
    {
        sb.Append("\tEnchanted={");
        for (int i = 0; i < effects.Length; i++)
        {
            if (i > 0) sb.Append(';');
            var e = effects[i];
            sb.Append(string.Create(CultureInfo.InvariantCulture,
                $"{{[{e.Effect}];{{{e.OffsetX:0.0###};{e.OffsetY:0.0###};{e.OffsetZ:0.0###}}};{{{e.MeshOffsetX:0.0###};{e.MeshOffsetY:0.0###};{e.MeshOffsetZ:0.0###}}};{{{e.MeshScaleX:0.0###};{e.MeshScaleY:0.0###};{e.MeshScaleZ:0.0###}}};{e.Velocity:0.0###};{e.ParticleScale:0.0###};{e.EffectScale:0.0###};{{{e.ParticleOffsetX:0.0###};{e.ParticleOffsetY:0.0###};{e.ParticleOffsetZ:0.0###}}};{{{e.RingOffsetX:0.0###};{e.RingOffsetY:0.0###};{e.RingOffsetZ:0.0###}}};{{{e.RingScaleX:0.0###};{e.RingScaleY:0.0###};{e.RingScaleZ:0.0###}}}}}"));
        }
        sb.Append('}');
    }
}

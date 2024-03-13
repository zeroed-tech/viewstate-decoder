using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace viewstate_decoder
{
    class ClassRepo
    {
        private static Dictionary<int, ReusableClass> repo = [];

        public static void AddClassDefinition(int id, ReusableClass clazz)
        {
            repo.Add(id, clazz);
        }

        public static ReusableClass GetClassWithId(int id)
        {
            if (repo.TryGetValue(id, out ReusableClass val))
            {
                return val;
            }
            throw new Exception($"Attempted to reference missing class {id}");
        }

        public static void Reset()
        {
            repo.Clear();
        }
    }

    public interface Parsable
    {
        public abstract Parsable Parse(Stream stream);
    }

    public interface ReusableClass
    {
        public object[] ReadValues(Stream stream);
    }

    public abstract class Record : Parsable
    {
        public abstract byte RecordType { get; }
        bool parsed = false;

        public bool ValidNextRecord(Stream stream)
        {
            return stream.ReadByte() == RecordType;
        }

        public virtual Parsable Parse(Stream stream)
        {
            if (!ValidNextRecord(stream))
            {
                stream.Position -= 1;
                throw new Exception($"Invalid record ID, expected {RecordType} but got {stream.ReadByte()}");
            }
            parsed = true;
            return this;
        }

        public virtual void AddToObjectGraph(ObjectManager objectManager)
        {

        }
    }

    /// <summary>
    /// 2.3.1.1 ClassInfo
    /// </summary>
    public class ClassInfo : Parsable
    {
        public int ObjectId;
        public string Name;
        public int MemberCount;
        public string[] MemberNames;

        public Parsable Parse(Stream stream)
        {
            ObjectId = stream.ReadInt32();
            Name = stream.ReadVarString();
            MemberCount = stream.ReadInt32();
            MemberNames = new string[MemberCount];
            for (int i = 0; i < MemberCount; i++)
            {
                MemberNames[i] = stream.ReadVarString();
            }
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.AppendLine($"  Name: {Name}");
            sb.AppendLine($"  MemberCount: {MemberCount}");
            if (MemberCount > 0)
            {
                sb.AppendLine("  Members:");
                for (int i = 0; i < MemberCount; i++)
                {
                    sb.AppendLine($"    [{i}] {MemberNames[i]}");
                }
            }
            return sb.ToString();
        }
    }

    public class ArrayInfo : Parsable
    {
        public int ObjectId;
        public int Length;

        public Parsable Parse(Stream stream)
        {
            ObjectId = stream.ReadInt32();
            Length = stream.ReadInt32();
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.Append($"  Length: {Length}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.1.1.8 ClassTypeInfo
    /// </summary>
    public class ClassTypeInfo : Parsable
    {
        public string LibraryName;
        public int LibraryId;

        public Parsable Parse(Stream stream)
        {
            LibraryName = stream.ReadVarString();
            LibraryId = stream.ReadInt32();
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  LibraryName: {LibraryName}");
            sb.AppendLine($"  LibraryId: {LibraryId}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.3.1.2 MemberTypeInfo
    /// </summary>
    public class MemberTypeInfo : Parsable
    {
        private readonly int expectedCount;
        public BinaryTypeEnum[] BinaryTypeEnums;
        public object?[] AdditionalInfos;

        public MemberTypeInfo(int expectedCount)
        {
            this.expectedCount = expectedCount;
        }

        public Parsable Parse(Stream stream)
        {
            BinaryTypeEnums = new BinaryTypeEnum[expectedCount];
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                BinaryTypeEnums[i] = (BinaryTypeEnum)stream.ReadByte();
            }

            AdditionalInfos = new object?[expectedCount];

            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                switch (BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.ObjectArray:
                    case BinaryTypeEnum.StringArray:
                        AdditionalInfos[i] = null;
                        break;
                    case BinaryTypeEnum.SystemClass:
                        AdditionalInfos[i] = stream.ReadVarString();
                        break;
                    case BinaryTypeEnum.Class:
                        //TODO
                        //AdditionalInfos[i] = new ClassTypeInfo(stream);
                        break;
                    case BinaryTypeEnum.Primitive:
                    case BinaryTypeEnum.PrimitiveArray:
                        AdditionalInfos[i] = (PrimitiveTypeEnumeration)stream.ReadByte();
                        break;
                }
            }
            return this;
        }

        public object[] ReadValues(Stream stream)
        {
            var Values = new object?[expectedCount];
            for (int i = 0; i < AdditionalInfos.Length; i++)
            {
                switch (BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.StringArray:
                    case BinaryTypeEnum.SystemClass:
                    case BinaryTypeEnum.ObjectArray:
                    case BinaryTypeEnum.PrimitiveArray:
                        var s = RecordMap.GetRecordHandler(stream.Peek());
                        s.Parse(stream);
                        Values[i] = s;
                        break;
                    case BinaryTypeEnum.Class:
                        var c = new ClassTypeInfo();
                        c.Parse(stream);
                        Values[i] = c;
                        break;
                    case BinaryTypeEnum.Primitive:
                        Values[i] = stream.ReadPrimitiveType((PrimitiveTypeEnumeration)AdditionalInfos[i]);
                        break;
                }
            }
            return Values;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine("  Types:");
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                sb.AppendLine($"    [{i}] {BinaryTypeEnums[i]}");
            }
            sb.AppendLine("  Additional Data:");
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                if (AdditionalInfos[i] != null)
                {
                    sb.AppendLine($"    [{i}] {AdditionalInfos[i]}");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.5.3 MemberReference
    /// </summary>
    class MemberReference : Record
    {
        public override byte RecordType { get => 9; }
        public int IdRef;
        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            IdRef = stream.ReadInt32();
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            objectManager.GetNodeWithId(IdRef);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.GetType().Name);
            sb.Append($" => IdRef: {IdRef}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.5.4 ObjectNull
    /// </summary>
    class ObjectNull : Record
    {
        public override byte RecordType { get => 10; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.GetType().Name);
            return sb.ToString();
        }
    }


    /// <summary>
    /// 2.6.1 SerializationHeaderRecord
    /// </summary>
    class SerializationHeaderRecord : Record
    {
        public override byte RecordType { get => 0; }
        public int RootId;
        public int HeaderId;
        public int MajorVersion;
        public int MinorVersion;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            RootId = stream.ReadInt32();
            HeaderId = stream.ReadInt32();
            MajorVersion = stream.ReadInt32();
            MinorVersion = stream.ReadInt32();
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  RootId: {RootId}");
            sb.AppendLine($"  Version: {MajorVersion}.{MinorVersion}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.6.2 BinaryLibrary
    /// </summary>
    class BinaryLibrary : Record
    {
        public override byte RecordType { get => 12; }
        public int LibraryId;
        public string LibraryName;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            LibraryId = stream.ReadInt32();
            LibraryName = stream.ReadVarString();
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = new ObjectNode
            {
                Id = LibraryId,
                Type = LibraryName
            };
            objectManager.AddLibrary(node);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  LibraryID: {LibraryId}");
            sb.AppendLine($"  LibraryName: {LibraryName}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.5.6 ObjectNullMultiple256
    /// </summary>
    class ObjectNullMultiple256 : Record
    {
        public override byte RecordType => 13;
        public byte NullCount;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            NullCount = (byte)stream.ReadByte();
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{this.GetType().Name} NullCount: {NullCount}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.4.3.3 ArraySinglePrimitive
    /// </summary>
    class ArraySinglePrimitive : Record
    {
        public override byte RecordType => 15;
        public ArrayInfo ArrayInfo;
        public PrimitiveTypeEnumeration PrimitiveTypeEnumeration;
        public object Value;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ArrayInfo = new ArrayInfo();
            ArrayInfo.Parse(stream);
            PrimitiveTypeEnumeration = (PrimitiveTypeEnumeration)stream.ReadByte();
            // Read the value of this array
            switch (PrimitiveTypeEnumeration)
            {
                case PrimitiveTypeEnumeration.Byte:
                    byte[] bytes = new byte[ArrayInfo.Length];
                    stream.Read(bytes);
                    if (bytes.Length > 17 && bytes[0] == 0)
                    {
                        // Bytes are long enough to contain a nested BF blob, try decoding the first section
                        using MemoryStream s = new MemoryStream(bytes);

                        new SerializationHeaderRecord().Parse(s);
                        s.Position = 0;
                        Console.WriteLine("Identified nested Binary Formatted blob, deserialising");
                        ClassRepo.Reset();
                        ObjectManager objectManager = new ObjectManager();
                        while (s.Position < s.Length)
                        {
                            byte recordId = s.Peek();
                            //Console.WriteLine($"==========Nested {recordId}============");
                            var record = RecordMap.GetRecordHandler(recordId);
                            record.Parse(s);
                            //Console.WriteLine(RecordManager.Indent($"{record}"));
                            record.AddToObjectGraph(objectManager);
                        }
                        Console.WriteLine(objectManager);

                    }
                    Value = bytes;
                    break;
                default:
                    throw new Exception("Unimplemented)");

            }
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ArrayInfo.ObjectId);
            node.Type = $"{PrimitiveTypeEnumeration}[]";
            node.Value = Value;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ArrayInfo: {RecordManager.Indent(ArrayInfo.ToString())}");
            sb.AppendLine($"  PrimitiveTypeEnumeration: {PrimitiveTypeEnumeration.ToString()}");
            sb.AppendLine($"  Value:");
            switch (PrimitiveTypeEnumeration)
            {
                case PrimitiveTypeEnumeration.Byte:
                    sb.Append(RecordManager.Indent(BitConverter.ToString((byte[])Value).Replace("-", string.Empty)));
                    break;
                default:
                    throw new Exception("Unimplemented)");

            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.4.3.2 ArraySingleObject
    /// </summary>
    class ArraySingleObject : Record
    {
        public override byte RecordType => 16;
        public ArrayInfo ArrayInfo;
        public object[] Values;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ArrayInfo = new ArrayInfo();
            ArrayInfo.Parse(stream);
            Values = new object[ArrayInfo.Length];
            for (int i = 0; i < Values.Length; i++)
            {
                Parsable record = RecordMap.GetRecordHandler(stream.Peek());
                record.Parse(stream);
                switch (record)
                {
                    case ObjectNullMultiple256 r:
                        {
                            // ObjectNullMultiple256 records contain a count which should be interpreted as the next n records being null
                            for (int j = 0; j < r.NullCount; j++)
                            {
                                Values[i + j] = r;
                            }
                            i += r.NullCount;
                            break;
                        }
                    default:
                        {
                            Values[i] = record;
                            break;
                        }
                }
            }
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ArrayInfo.ObjectId);
            for(int i = 0; i < ArrayInfo.Length; i++)
            {
                switch(Values[i])
                {
                    case MemberReference m:
                        {
                            var memberNode = objectManager.GetNodeWithId(m.IdRef);
                            memberNode.Name = $"{i}";

                            node.Members.Add(memberNode);
                            break;
                        }
                    case null:
                    case ObjectNull:
                    case ObjectNullMultiple256:
                        {
                            node.Members.Add(new ObjectNode { Type = "Null", Name = $"{i}", Value = "Null" });
                            break;
                        }
                    case MemberPrimitiveTyped m:
                        {

                            node.Members.Add(new ObjectNode
                            {
                                Name = $"{i}",
                                Type = m.PrimitiveType.ToString(),
                                Value = m.Value,
                            });
                            break;
                        }
                    case BinaryObjectString b:
                        {
                            var memberNode = objectManager.GetNodeWithId(b.ObjectId);
                            memberNode.Type = "String";
                            memberNode.Value = b.Value;

                            node.Members.Add(memberNode);
                            break;
                        }
                    default:
                        break;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ArrayInfo: {RecordManager.Indent(ArrayInfo.ToString())}");
            sb.AppendLine($"  Values:");
            for (int i = 0; i < Values.Length; i++)
            {

                if (Values[i] != null)
                {
                    sb.AppendLine($"    [{i}] {RecordManager.Indent(Values[i].ToString())}");
                }
                else
                {
                    sb.AppendLine($"    [{i}] {RecordManager.Indent("Null")}");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.3.2.1 ClassWithMembersAndTypes
    /// </summary>
    class ClassWithMembersAndTypes : Record, ReusableClass
    {
        public override byte RecordType { get => 5; }
        public ClassInfo ClassInfo;
        public MemberTypeInfo MemberTypeInfo;
        public int LibraryId;
        public object[] Values;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
            ClassRepo.AddClassDefinition(ClassInfo.ObjectId, this);
            MemberTypeInfo = new MemberTypeInfo(ClassInfo.MemberCount);
            MemberTypeInfo.Parse(stream);
            LibraryId = stream.ReadInt32();
            Values = ReadValues(stream);
            return this;
        }

        public object[] ReadValues(Stream stream)
        {
            return MemberTypeInfo.ReadValues(stream);
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ClassInfo.ObjectId);
            objectManager.AddClassToLibrary(LibraryId, node);
            node.Type = ClassInfo.Name;

            for (int i = 0; i < ClassInfo.MemberCount; i++)
            {
                switch (MemberTypeInfo.BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.StringArray:
                    case BinaryTypeEnum.SystemClass:
                    case BinaryTypeEnum.ObjectArray:
                    case BinaryTypeEnum.PrimitiveArray:
                        switch (Values[i])
                        {
                            case MemberReference m:
                                var memberNode = objectManager.GetNodeWithId(m.IdRef);
                                memberNode.Name = ClassInfo.MemberNames[i];

                                node.Members.Add(memberNode);
                                break;
                            default: 
                                break;
                        }
                        break;
                    case BinaryTypeEnum.Class:
                        break;
                    case BinaryTypeEnum.Primitive:
                        //Values[i] = stream.ReadPrimitiveType((PrimitiveTypeEnumeration)AdditionalInfos[i]);
                        break;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  {RecordManager.Indent(ClassInfo.ToString())}");
            sb.AppendLine($"  {RecordManager.Indent(MemberTypeInfo.ToString())}");
            sb.AppendLine($"  LibraryId: {LibraryId}");
            sb.AppendLine("  Values:");
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] != null)
                {
                    sb.AppendLine($"    [{i}] {RecordManager.Indent(Values[i].ToString())}");
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.4.3.4 ArraySingleString
    /// </summary>
    class ArraySingleString : Record
    {
        public override byte RecordType { get => 17; }
        public ArrayInfo ArrayInfo;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ArrayInfo = new ArrayInfo { ObjectId = stream.ReadInt32(), Length = stream.ReadInt32() };
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ArrayInfo:");
            sb.AppendLine($"  {RecordManager.Indent(ArrayInfo.ToString())}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.5.7 BinaryObjectString
    /// </summary>
    class BinaryObjectString : Record
    {
        public override byte RecordType { get => 6; }
        public int ObjectId;
        public string Value;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ObjectId = stream.ReadInt32();
            Value = stream.ReadVarString();
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.AppendLine($"  Value: {Value}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.4.3.1 BinaryArray
    /// </summary>
    class BinaryArray : Record
    {
        public override byte RecordType => 7;
        public int ObjectId;
        public BinaryArrayTypeEnum BinaryArrayTypeEnum;
        public int Rank;
        public int[] Lengths;
        public int[] LowerBounds;
        public BinaryTypeEnum TypeEnum;
        public object? AdditionalInfos;
        public object[] Values;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ObjectId = stream.ReadInt32();
            BinaryArrayTypeEnum = (BinaryArrayTypeEnum)stream.ReadByte();
            Rank = stream.ReadInt32();
            Lengths = new int[Rank];
            for (int i = 0; i < Rank; i++)
            {
                Lengths[i] = stream.ReadInt32();
            }

            if (BinaryArrayTypeEnum == BinaryArrayTypeEnum.SingleOffset || BinaryArrayTypeEnum == BinaryArrayTypeEnum.JaggedOffset || BinaryArrayTypeEnum == BinaryArrayTypeEnum.RectangularOffset)
            {
                LowerBounds = new int[Rank];
                for (int i = 0; i < Rank; i++)
                {
                    LowerBounds[i] = stream.ReadInt32();
                }
            }

            TypeEnum = (BinaryTypeEnum)stream.ReadByte();

            switch (TypeEnum)
            {
                case BinaryTypeEnum.String:
                case BinaryTypeEnum.Object:
                case BinaryTypeEnum.ObjectArray:
                case BinaryTypeEnum.StringArray:
                    AdditionalInfos = null;
                    break;
                case BinaryTypeEnum.SystemClass:
                    AdditionalInfos = stream.ReadVarString();
                    break;
                case BinaryTypeEnum.Class:
                    //TODO
                    //AdditionalInfos[i] = new ClassTypeInfo(stream);
                    break;
                case BinaryTypeEnum.Primitive:
                case BinaryTypeEnum.PrimitiveArray:
                    AdditionalInfos = (PrimitiveTypeEnumeration)stream.ReadByte();
                    break;
            }
            int totalLength = 0;
            for (int i = 0; i < Lengths.Length; i++)
            {
                totalLength += Lengths[i];
            }
            Values = new object[totalLength];
            for (int i = 0; i < Lengths.Length; i++)
            {
                for (int j = 0; j < Lengths[i]; j++)
                {
                    switch (TypeEnum)
                    {
                        case BinaryTypeEnum.String:
                        case BinaryTypeEnum.Object:
                        case BinaryTypeEnum.ObjectArray:
                        case BinaryTypeEnum.StringArray:

                            break;
                        case BinaryTypeEnum.SystemClass:

                            break;
                        case BinaryTypeEnum.Class:
                            //TODO
                            //AdditionalInfos[i] = new ClassTypeInfo(stream);
                            break;
                        case BinaryTypeEnum.Primitive:
                            break;
                        case BinaryTypeEnum.PrimitiveArray:
                            switch ((PrimitiveTypeEnumeration)AdditionalInfos)
                            {
                                case PrimitiveTypeEnumeration.Byte:
                                    var r = RecordMap.GetRecordHandler(stream.Peek());
                                    r.Parse(stream);
                                    Values[(i + 1) * j] = r;
                                    break;
                                default:
                                    throw new Exception("Not implemented");
                            }
                            break;
                    }
                }
            }
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ObjectId);
            switch (TypeEnum)
            {
                case BinaryTypeEnum.SystemClass:
                    node.Type = "SystemClass";
                    break;
                case BinaryTypeEnum.Primitive:
                    node.Type = AdditionalInfos.ToString(); 
                    break;
                case BinaryTypeEnum.PrimitiveArray:
                    node.Type = $"{AdditionalInfos}[]";
                    break;
            }
            for(int i = 0; i < Values.Length; i++)
            {
                switch(Values[i])
                {
                    case MemberReference m:
                        {
                            node.AddMember(objectManager.GetNodeWithId(m.IdRef));
                            break;
                        }
                    default:
                        break;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.AppendLine($"  BinaryArrayTypeEnum: {BinaryArrayTypeEnum}");
            sb.AppendLine($"  Rank: {Rank}");
            sb.AppendLine($"  Lengths:");
            for (int i = 0; i < Rank; i++)
            {
                sb.AppendLine($"    [{i}] {Lengths[i]}");
            }
            if (BinaryArrayTypeEnum == BinaryArrayTypeEnum.SingleOffset || BinaryArrayTypeEnum == BinaryArrayTypeEnum.JaggedOffset || BinaryArrayTypeEnum == BinaryArrayTypeEnum.RectangularOffset)
            {
                sb.AppendLine($"  LowerBounds:");
                for (int i = 0; i < Rank; i++)
                {
                    sb.Append($"    [{i}] {LowerBounds[i]}");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"  TypeEnum: {TypeEnum.ToString()}");
            sb.AppendLine($"  AdditionalInfos: {AdditionalInfos.ToString()}");
            sb.AppendLine($"  Values:");
            for (int i = 0; i < Values.Length; i++)
            {
                sb.AppendLine($"    [{i}] {Values[i]}");
            }
            return sb.ToString();
        }
    }


    /// <summary>
    /// 2.5.1 MemberPrimitiveTyped
    /// </summary>
    class MemberPrimitiveTyped : Record
    {
        public override byte RecordType => 8;
        public PrimitiveTypeEnumeration PrimitiveType;
        public object Value;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            PrimitiveType = (PrimitiveTypeEnumeration)stream.ReadByte();
            Value = stream.ReadPrimitiveType(PrimitiveType);
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  PrimitiveType: {PrimitiveType.ToString()}");
            sb.AppendLine($"  Value: {RecordManager.Indent(Value.ToString())}");
            return sb.ToString();
        }
    }


    class MessageEnd : Record
    {
        public override byte RecordType { get => 11; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.3.2.4 SystemClassWithMembers
    /// </summary>
    class SystemClassWithMembers : Record
    {
        public override byte RecordType { get => 2; }
        public ClassInfo ClassInfo;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
            return this;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ClassInfo:");
            sb.Append(RecordManager.Indent(ClassInfo.ToString()));
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.3.2.3 SystemClassWithMembersAndTypes
    /// </summary>
    class SystemClassWithMembersAndTypes : Record, ReusableClass
    {
        public override byte RecordType { get => 4; }
        public ClassInfo ClassInfo;
        public MemberTypeInfo MemberTypeInfo;
        public object[] Values;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
            ClassRepo.AddClassDefinition(ClassInfo.ObjectId, this);
            MemberTypeInfo = new MemberTypeInfo(ClassInfo.MemberCount);
            MemberTypeInfo.Parse(stream);
            Values = ReadValues(stream);
            return this;
        }

        public object[] ReadValues(Stream stream)
        {
            return MemberTypeInfo.ReadValues(stream);
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ClassInfo.ObjectId);
            objectManager.AddSystemClass(node);
            node.Type = ClassInfo.Name;

            for (int i = 0; i < ClassInfo.MemberCount; i++)
            {
                switch (MemberTypeInfo.BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.StringArray:
                    case BinaryTypeEnum.ObjectArray:
                    case BinaryTypeEnum.PrimitiveArray:
                        switch (Values[i])
                        {
                            case MemberReference r:
                                {
                                    var memberNode = objectManager.GetNodeWithId(r.IdRef);
                                    memberNode.Name = ClassInfo.MemberNames[i];

                                    node.Members.Add(memberNode);
                                    break;
                                }
                            case BinaryObjectString s:
                                {
                                    var memberNode = objectManager.GetNodeWithId(s.ObjectId);
                                    memberNode.Name = ClassInfo.MemberNames[i];
                                    memberNode.Type = "String";
                                    memberNode.Value = s.Value;

                                    node.Members.Add(memberNode);
                                    break;
                                }
                            case ObjectNull:
                                {
                                    node.Members.Add(new ObjectNode
                                    {
                                        Name = ClassInfo.MemberNames[i],
                                        Type = "Object",
                                        Value = "Null"
                                    });
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    case BinaryTypeEnum.Class:
                        break;
                    case BinaryTypeEnum.SystemClass:
                    case BinaryTypeEnum.Primitive:
                        {
                            //Values[i] = stream.ReadPrimitiveType((PrimitiveTypeEnumeration)AdditionalInfos[i]);
                            node.Members.Add(new ObjectNode
                            {
                                Name = ClassInfo.MemberNames[i],
                                Type = MemberTypeInfo.BinaryTypeEnums[i].ToString(),
                                Value = Values[i]
                            });
                        break;
                        }
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.Append(RecordManager.Indent(ClassInfo.ToString()));
            sb.Append(RecordManager.Indent(MemberTypeInfo.ToString()));
            sb.AppendLine("  Values:");
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] != null)
                {
                    sb.AppendLine($"    [{i}] {RecordManager.Indent(Values[i].ToString())}");
                }
            }
            return sb.ToString();
        }


    }

    /// <summary>
    /// 2.3.2.5 ClassWithId
    /// </summary>
    class ClassWithId : Record
    {
        public override byte RecordType { get => 1; }
        public int ObjectId;
        public int MetadataId;
        public object[] Values;

        public override Parsable Parse(Stream stream)
        {
            base.Parse(stream);
            ObjectId = stream.ReadInt32();
            MetadataId = stream.ReadInt32();

            // Retrieve the referenced class
            var clazz = ClassRepo.GetClassWithId(MetadataId);
            Values = clazz.ReadValues(stream);
            return this;
        }

        public override void AddToObjectGraph(ObjectManager objectManager)
        {
            var node = objectManager.GetNodeWithId(ObjectId);
            var clazz = ClassRepo.GetClassWithId(MetadataId);
            ClassInfo? ClassInfo = null;
            MemberTypeInfo? MemberTypeInfo = null;
            if(clazz is SystemClassWithMembersAndTypes)
            {
                var c = (SystemClassWithMembersAndTypes)clazz;
                ClassInfo = c.ClassInfo;
                MemberTypeInfo = c.MemberTypeInfo;
            }else if (clazz is ClassWithMembersAndTypes)
            {
                var c = (ClassWithMembersAndTypes)clazz;
                ClassInfo = c.ClassInfo;
                MemberTypeInfo = c.MemberTypeInfo;
            }

            node.Type = ClassInfo.Name;

            for (int i = 0; i < ClassInfo.MemberCount; i++)
            {
                switch (MemberTypeInfo.BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.StringArray:
                    case BinaryTypeEnum.SystemClass:
                    case BinaryTypeEnum.ObjectArray:
                    case BinaryTypeEnum.PrimitiveArray:
                        switch (Values[i])
                        {
                            case MemberReference m:
                                {
                                    var memberNode = objectManager.GetNodeWithId(m.IdRef);
                                    memberNode.Name = ClassInfo.MemberNames[i];

                                    node.Members.Add(memberNode);
                                    break;
                                }
                            case BinaryObjectString b:
                                {
                                    var memberNode = objectManager.GetNodeWithId(b.ObjectId);
                                    memberNode.Name = ClassInfo.MemberNames[i];
                                    memberNode.Value = b.Value;

                                    node.Members.Add(memberNode);
                                    break;
                                }
                            case ObjectNull:
                                {
                                    node.Members.Add(new ObjectNode { 
                                        Type = MemberTypeInfo.BinaryTypeEnums[i].ToString(), 
                                        Name = ClassInfo.MemberNames[i], 
                                        Value = "Null" });
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    case BinaryTypeEnum.Class:
                        break;
                    case BinaryTypeEnum.Primitive:
                        node.Members.Add(new ObjectNode
                        {
                            Name = ClassInfo.MemberNames[i],
                            Type = MemberTypeInfo.AdditionalInfos[i].ToString(),
                            Value = Values[i]
                        });
                        break;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.AppendLine($"  MetadataId: {MetadataId}");
            sb.AppendLine("  Values:");
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] != null)
                {
                    sb.AppendLine($"    [{i}] {RecordManager.Indent(Values[i].ToString())}");
                }
            }
            return sb.ToString();
        }
    }

    public class RecordMap
    {
        private Dictionary<byte, Type> recordMap = new Dictionary<byte, Type>();

        private static RecordMap instance;

        public static Record GetRecordHandler(byte id)
        {
            if(instance == null)
            {
                instance = new RecordMap();
                // Init record map
                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(t => typeof(Record).IsAssignableFrom(t) && !t.IsAbstract)
                    .ToList()
                    .ForEach(t =>
                    {
                        var i = (Record)Activator.CreateInstance(t)!;
                        instance.recordMap.Add(i.RecordType, t);
                    });
            }

            if(instance.recordMap.TryGetValue(id, out var type))
            {
                return (Record)Activator.CreateInstance(type)!;
            }
            else
            {
                throw new Exception($"Unknown ID {id}");
            }
        }
    }

    public class RecordManager
    {
        public static string Indent(string s)
        {
            return $"  {s.Replace("\n", "\n  ")}";
        }
    }
}

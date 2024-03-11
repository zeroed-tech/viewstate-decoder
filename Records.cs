using System.Text;

namespace viewstate_decoder
{

    public interface Parsable
    {
        public abstract void Parse(Stream stream);
    }

    public abstract class Record : Parsable
    {
        public abstract byte RecordType { get; }
        bool parsed = false;

        public bool ValidNextRecord(Stream stream)
        {
            return stream.ReadByte() == RecordType;
        }

        public virtual void Parse(Stream stream)
        {
            if (!ValidNextRecord(stream))
            {
                stream.Position -= 1;
                throw new Exception($"Invalid record ID, expected {RecordType} but got {stream.ReadByte()}");
            }
            parsed = true;
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

        public void Parse(Stream stream)
        {
            ObjectId = stream.ReadInt32();
            Name = stream.ReadVarString();
            MemberCount = stream.ReadInt32();
            MemberNames = new string[MemberCount];
            for (int i = 0; i < MemberCount; i++)
            {
                MemberNames[i] = stream.ReadVarString();
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"    ObjectId: {ObjectId}");
            sb.AppendLine($"    Name: {Name}");
            sb.AppendLine($"    MemberCount: {MemberCount}");
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

    /// <summary>
    /// 2.1.1.8 ClassTypeInfo
    /// </summary>
    public class ClassTypeInfo : Parsable
    {
        public string LibraryName;
        public int LibraryId;

        public void Parse(Stream stream)
        {
            LibraryName = stream.ReadVarString();
            LibraryId = stream.ReadInt32();
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
        public object?[] Values;

        public MemberTypeInfo(int expectedCount)
        {
            this.expectedCount = expectedCount;
        }

        public void Parse(Stream stream)
        {
            BinaryTypeEnums = new BinaryTypeEnum[expectedCount];
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                BinaryTypeEnums[i] = (BinaryTypeEnum)stream.ReadByte();
            }

            AdditionalInfos = new object?[expectedCount];
            Values = new object?[expectedCount];

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
        }

        public void ReadValues(Stream stream)
        {
            for (int i = 0; i < AdditionalInfos.Length; i++)
            {
                switch (BinaryTypeEnums[i])
                {
                    case BinaryTypeEnum.String:
                    case BinaryTypeEnum.Object:
                    case BinaryTypeEnum.StringArray:
                    case BinaryTypeEnum.SystemClass:
                        var s = RecordManager.ParseRecord(stream.Peek(), stream);
                        Values[i] = s;
                        break;
                    case BinaryTypeEnum.ObjectArray:
                        break;
                    case BinaryTypeEnum.Class:
                        var c = new ClassTypeInfo();
                        c.Parse(stream);
                        Values[i] = c;
                        break;
                    case BinaryTypeEnum.Primitive:
                        switch ((PrimitiveTypeEnumeration)AdditionalInfos[i]!)
                        {
                            case PrimitiveTypeEnumeration.Int32:
                                Values[i] = stream.ReadInt32();
                                break;
                            default:
                                Console.WriteLine($"Unhandled const {((PrimitiveTypeEnumeration)AdditionalInfos[i]!).ToString()}");
                                break;

                        }
                        break;
                    case BinaryTypeEnum.PrimitiveArray:
                        break;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("   Types");
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                sb.AppendLine($"    [{i}] {BinaryTypeEnums[i]}");
            }
            sb.AppendLine("   Additional Data");
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                if (AdditionalInfos[i] != null)
                {
                    sb.AppendLine($"    [{i}] {AdditionalInfos[i]}");
                }
            }
            sb.AppendLine("   Values");
            for (int i = 0; i < BinaryTypeEnums.Length; i++)
            {
                if (Values[i] != null)
                {
                    sb.AppendLine($"    [{i}] {Values[i]}");
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
        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            IdRef = stream.ReadInt32();
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
            sb.AppendLine(this.GetType().Name);
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            RootId = stream.ReadInt32();
            HeaderId = stream.ReadInt32();
            MajorVersion = stream.ReadInt32();
            MinorVersion = stream.ReadInt32();
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            LibraryId = stream.ReadInt32();
            LibraryName = stream.ReadVarString();
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
    /// 2.3.2.1 ClassWithMembersAndTypes
    /// </summary>
    class ClassWithMembersAndTypes : Record
    {
        public override byte RecordType { get => 5; }
        public ClassInfo ClassInfo;
        public MemberTypeInfo MemberTypeInfo;
        public int LibraryId;

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
            MemberTypeInfo = new MemberTypeInfo(ClassInfo.MemberCount);
            MemberTypeInfo.Parse(stream);
            LibraryId = stream.ReadInt32();
            MemberTypeInfo.ReadValues(stream);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ClassInfo:");
            sb.Append(ClassInfo.ToString());
            sb.AppendLine($"  MemberTypeInfo:");
            sb.AppendLine(MemberTypeInfo.ToString());
            sb.AppendLine($"  LibraryId: {LibraryId}");
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ArrayInfo = new ArrayInfo { ObjectId = stream.ReadInt32(), Length = stream.ReadInt32() };
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ArrayInfo:");
            sb.AppendLine(ArrayInfo.ToString());
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ObjectId = stream.ReadInt32();
            Value = stream.ReadVarString();
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
    /// 2.5.1 MemberPrimitiveTyped

    /// </summary>
    class MemberPrimitiveTyped : Record
    {
        public override byte RecordType => 8;
        public PrimitiveTypeEnumeration PrimitiveType;
        public object Value;

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            PrimitiveType = (PrimitiveTypeEnumeration)stream.ReadByte();
            Value = stream.ReadPrimitiveType(PrimitiveType);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"PrimitiveType: {PrimitiveType.ToString()}");
            sb.AppendLine($"Value: {Value}");
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ClassInfo:");
            sb.Append(ClassInfo.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.3.2.3 SystemClassWithMembersAndTypes
    /// </summary>
    class SystemClassWithMembersAndTypes : Record
    {
        public override byte RecordType { get => 4; }
        public ClassInfo ClassInfo;
        public MemberTypeInfo MemberTypeInfo;

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ClassInfo = new ClassInfo();
            ClassInfo.Parse(stream);
            MemberTypeInfo = new MemberTypeInfo(ClassInfo.MemberCount);
            MemberTypeInfo.Parse(stream);
            MemberTypeInfo.ReadValues(stream);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ClassInfo:");
            sb.Append(ClassInfo.ToString());
            sb.AppendLine($"  MemberTypeInfo:");
            sb.AppendLine(MemberTypeInfo.ToString());
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

        public override void Parse(Stream stream)
        {
            base.Parse(stream);
            ObjectId = stream.ReadInt32();
            MetadataId = stream.ReadInt32();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().Name);
            sb.AppendLine($"  ObjectId: {ObjectId}");
            sb.AppendLine($"  MetadataId: {MetadataId}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// 2.2.3.1 BinaryMethodCall
    /// </summary>
    //class BinaryMethodCall
    //{
    //    public int MessageEnum;
    //    public string MethodName;
    //    public string TypeName;
    //    public string CallContext;
    //    public string[] Args;

    //    public BinaryMethodCall(Stream stream)
    //    {
    //        MessageEnum = stream.ReadInt32();
    //        MethodName = new StringValueWithCode(stream).Value;
    //        TypeName = new StringValueWithCode(stream).Value;
    //        CallContext = new StringValueWithCode(stream).Value;
    //    }
    //}

    #region PrimitiveRecords

    /// <summary>
    /// 2.2.2.2 StringValueWithCode
    /// </summary>
    public class StringValueWithCode
    {
        public string Value;
        public StringValueWithCode(Stream stream)
        {
            if ((PrimitiveTypeEnumeration)stream.ReadByte() != PrimitiveTypeEnumeration.String)
            {
                throw new Exception("Expected string contantant");
            }
            this.Value = stream.ReadVarString();
        }
    }

    #endregion

    public class RecordManager
    {
        public static Parsable? ParseRecord(byte id, Stream stream)
        {
            Parsable record = null;
            switch (id)
            {
                case 0:
                    {
                        record = new SerializationHeaderRecord();
                        break;
                    }
                case 1:
                    {
                        record = new ClassWithId();
                        break;
                    }
                case 2:
                    {
                        record = new SystemClassWithMembers();

                        break;
                    }
                case 4:
                    {
                        record = new SystemClassWithMembersAndTypes();

                        break;
                    }
                case 5:
                    {
                        record = new ClassWithMembersAndTypes();

                        break;
                    }
                case 6:
                    {
                        record = new BinaryObjectString();

                        break;
                    }
                case 8:
                        record = new MemberPrimitiveTyped();
                        break;
                case 9:
                    {
                        record = new MemberReference();
                        break;
                    }
                case 10:
                    {
                        record = new ObjectNull();
                        break;
                    }
                case 11:
                    {
                        record = new MessageEnd();

                        break;
                    }
                case 12:
                    {
                        record = new BinaryLibrary();

                        break;
                    }
                case 17:
                    {
                        record = new ArraySingleString();
                        break;
                    }
                //case 21:
                //    {
                //        record = new BinaryMethodCall(stream);
                //        
                //        break;
                //    }
                default:
                    throw new Exception($"Invalid type {id}");
                    break;
            }
            if (record != null)
            {
                record.Parse(stream);
            }
            return record;
        }
    }
}

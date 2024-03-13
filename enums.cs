namespace viewstate_decoder
{
    public enum BinaryTypeEnum
    {
        Primitive = 0,
        String = 1,
        Object = 2,
        SystemClass = 3,
        Class = 4,
        ObjectArray = 5,
        StringArray = 6,
        PrimitiveArray = 7
    }

    public enum PrimitiveTypeEnumeration
    {
        Boolean = 1,
        Byte = 2,
        Char = 3,
        Decimal = 5,
        Double = 6,
        Int16 = 7,
        Int32 = 8,
        Int64 = 9,
        SByte = 10,
        Single = 11,
        TimeSpan = 12,
        DateTime = 13,
        UInt16 = 14,
        UInt32 = 15,
        UInt64 = 16,
        Null = 17,
        String = 18,
    }

    public enum MessageFlags
    {
        NoArgs = 0x00000001,
        ArgsInline = 0x00000002,
        ArgsIsArray = 0x00000004,
        ArgsInArray = 0x00000008,
        NoContext = 0x00000010,
        ContextInline = 0x00000020,
        ContextInArray = 0x00000040,
        MethodSignatureInArray = 0x00000080,
        PropertiesInArray = 0x00000100,
        NoReturnValue = 0x00000200,
        ReturnValueVoid = 0x00000400,
        ReturnValueInline = 0x00000800,
        ReturnValueInArray = 0x00001000,
        ExceptionInArray = 0x00002000,
        GenericMethod = 0x00008000,
    }

    enum BinaryArrayTypeEnum
    {
        Single = 0,
        Jagged,
        Rectangular,
        SingleOffset,
        JaggedOffset,
        RectangularOffset,
    }
}

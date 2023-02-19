namespace ArrayCreation.CSharp
{
    public static class CreateArrayLib
    {
        public static Object[] CreateArray(int n) => new Object[n];

        public static Object[] CreateArrayFixed() => new object[32];

    }
}
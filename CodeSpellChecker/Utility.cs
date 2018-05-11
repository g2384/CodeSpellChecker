namespace CodeSpellChecker
{
    public class Utility
    {
        public static void Swap<T>(ref T arg1, ref T arg2)
        {
            T temp = arg1;
            arg1 = arg2;
            arg2 = temp;
        }
    }
}

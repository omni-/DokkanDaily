namespace DokkanDaily.Repository.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DataTableIndex : Attribute
    {
        public int Index { get; }

        public DataTableIndex(int index) 
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            Index = index;
        }
    }
}

using FastMember;

namespace DokkanDaily.Helpers
{
    public static class DatabaseHelper
    {
        public static T GetMemberAttribute<T>(this Member member) where T : Attribute
        {
            return member.GetAttribute(typeof(T), false) as T;
        }
    }
}

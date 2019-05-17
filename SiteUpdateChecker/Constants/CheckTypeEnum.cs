using EnumStringValues;

namespace SiteUpdateChecker.Constants
{
    public enum CheckTypeEnum
    {
        [StringValue("1")]
        ETag,

        [StringValue("2")]
        LastModified,

        [StringValue("3")]
        HtmlHash
    }
}

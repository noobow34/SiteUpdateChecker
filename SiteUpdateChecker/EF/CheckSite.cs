using SiteUpdateChecker.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace SiteUpdateChecker.EF
{
    [Table("CHECK_SITE")]
    public class CheckSite
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("SITE_ID")]
        public int SiteId { get; set; }

        [Column("URL")]
        public string Url { get; set; }

        [Column("SITE_NAME")]
        public string SiteName { get; set; }

        [Column("CHECK_TYPE")]
        public CheckTypeEnum? CheckType { get; set; }

        [Column("CHECK_IDENTIFIER")]
        public string CheckIdentifier { get; set; }

        [Column("LAST_CHECK")]
        public DateTime? LastCheck { get; set; }

        [Column("LAST_UPDATE")]
        public DateTime? LastUpdate { get; set; }
    }
}

namespace PeliplaneettaParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("peliplaneetta.Thread")]
    public partial class Thread
    {
        public int Id { get; set; }

        public int SpaceId { get; set; }

        public Space Space { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Column(TypeName = "bit")]
        public bool IsLocked { get; set; }

        public int PageCount { get; set; }

        public int OriginalThreadId { get; set; }
    }
}

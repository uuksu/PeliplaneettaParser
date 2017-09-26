namespace PeliplaneettaParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("peliplaneetta.Space")]
    public partial class Space
    {
        public int Id { get; set; }

        [Required]
        public int OriginalSpaceId { get; set; }

        [Required]
        [StringLength(50)]
        public string Title { get; set; }

        [Required]
        [StringLength(120)]
        public string Description { get; set; }
    }
}

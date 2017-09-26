namespace PeliplaneettaParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("peliplaneetta.Avatar")]
    public partial class Avatar
    {
        public int Id { get; set; }

        [Required]
        [StringLength(25)]
        public string Filename { get; set; }
    }
}

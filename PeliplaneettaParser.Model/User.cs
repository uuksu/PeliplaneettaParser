namespace PeliplaneettaParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("peliplaneetta.User")]
    public partial class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string Nickname { get; set; }

        [Column(TypeName = "date")]
        public DateTime RegisterationDate { get; set; }

        [StringLength(1000)]
        public string Signature { get; set; }

        public int? AvatarId { get; set; }

        public Avatar Avatar { get; set; }

        public int RoleId { get; set; }

        public Role Role { get; set; }

        public int OriginalUserId { get; set; }
    }
}

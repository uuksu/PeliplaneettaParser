namespace PeliplaneettaParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("peliplaneetta.Message")]
    public partial class Message
    {
        public int Id { get; set; }

        public int ThreadId { get; set; }

        public Thread Thread { get; set; }

        public int MessageIndex { get; set; }

        public int UserId { get; set; }

        public User User { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime CreationDateTime { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? EditDateTime { get; set; }

        public int OriginalMessageId { get; set; }

        [Column(TypeName = "text")]
        [StringLength(65535)]
        public string Text { get; set; }

        public int Page { get; set; }
    }
}

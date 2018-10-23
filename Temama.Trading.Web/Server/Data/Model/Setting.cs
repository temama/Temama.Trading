using System.ComponentModel.DataAnnotations;

namespace Temama.Trading.Web.Server.Data.Model
{
    public class Setting
    {
        [Key]
        public string Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; }

        [Required]
        [MaxLength(4096)]
        public string Value { get; set; }
    }
}

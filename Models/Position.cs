using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    [Table("Position")] 
    public class Position
    {
        [Key]
        public int pos_id { get; set; }
        public string pos_name { get; set; } = null!;
        public string? description { get; set; }
        public int min_manpower { get; set; }
    }
}
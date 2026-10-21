using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    [Table("Time_Slot")]
    public class TimeSlot
    {
        [Key]
        public int slot_id { get; set; }
        public string slot_name { get; set; } = null!;
        public TimeSpan start_time { get; set; }
        public TimeSpan end_time { get; set; }
    }
}
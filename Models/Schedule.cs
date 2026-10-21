// Models/Schedule.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    [Table("Schedule")]
    public class Schedule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int sche_id { get; set; }

        public int emp_id { get; set; }
        public int pos_id { get; set; }
        public int slot_id { get; set; }
        public int? manager_id { get; set; }
        public DateTime sche_date { get; set; }
        public string status { get; set; } = "Scheduled";

        [ForeignKey("emp_id")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("slot_id")]
        public virtual TimeSlot? TimeSlot { get; set; }

        [NotMapped]
        public string attendance_status { get; set; } = "Normal";
    }
}
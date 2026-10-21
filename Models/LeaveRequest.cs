using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    [Table("Leave_Request")] 
    public class LeaveRequest
    {
        [Key]
        public int leave_id { get; set; }
        public int emp_id { get; set; }
        public DateTime leave_date { get; set; }
        public string? leave_type { get; set; }
        public string status { get; set; } = "Pending";

        [ForeignKey("emp_id")]
        public virtual Employee? Employee { get; set; }
    }
}
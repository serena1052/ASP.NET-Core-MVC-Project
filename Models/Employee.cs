using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    [Table("Employee")]
    public class Employee
    {
        [Key]
        public int emp_id { get; set; }
        public string name { get; set; } = null!;
        public string? phone { get; set; }
        public string? job_title { get; set; }
        public string? employment_type { get; set; }

        public decimal salary { get; set; }

        public int? manager_id { get; set; }
    }
}
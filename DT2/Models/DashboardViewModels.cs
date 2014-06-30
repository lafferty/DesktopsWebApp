using System.ComponentModel.DataAnnotations;
using DotNet.Highcharts;

namespace DT2.Models
{
    public class DashboardViewModel
    {
        [Display(Name = "Active Users")]
        public int ActiveUsers { get; set; }
        
        [Display(Name = "Desktop Groups")]
        public int DesktopGroups { get; set; }
        
        [Display(Name = "Desktops")]
        public int Desktops { get; set; }

        [Display(Name = "Desktop Images")]
        public int DesktopImages { get; set; }

        [Display(Name = "Desktop Images")]
        public Highcharts PieChart { get; set; }
    }
}

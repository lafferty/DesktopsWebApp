using System.ComponentModel.DataAnnotations;

namespace DT2.Models
{
    public class CatalogDetailsViewModel
    {
        public Catalog CatalogInfo { get; set; }

        public Machine[] Machines { get; set; }
   }
}

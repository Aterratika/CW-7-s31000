using System.ComponentModel.DataAnnotations;

namespace CW7_S31000.Models.DTOs;

public class CountryGetDTO
{
    [Key]
    public int IdCountry { get; set; }
    
    [MaxLength(120)]
    public string Name { get; set; }
}
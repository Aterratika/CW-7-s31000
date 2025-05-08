using System.ComponentModel.DataAnnotations;

namespace CW7_S31000.Models.DTOs;

public class ClientTripGetDTO
{
    [Key]
    public int IdTrip { get; set; }
    public int RegisteredAt { get; set; }
    public int? PaymentDate { get; set; }
    public string Name { get; set; }
    
    [MaxLength(220)]
    public string Description { get; set; }
    
    public DateTime DateFrom { get; set; }
    
    public DateTime DateTo { get; set; }
    public int MaxPeople { get; set; }
}
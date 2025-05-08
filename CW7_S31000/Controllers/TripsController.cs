using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CW7_S31000.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TripsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TripsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Zwraca listę wszystkich wycieczek wraz z informacjami o nich i krajach, które odwiedzają.
        /// Endpoint: GET /api/trips
        /// </summary>
        [HttpGet]
        public IActionResult GetTrips()
        {
            var trips = new List<object>();

            // laczy z bazą danych
            using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            // pobiera wszystkie wycieczki i ich dane:
            using var command = new SqlCommand(@"
                SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, c.Name AS Country
                FROM Trip t
                LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
                LEFT JOIN Country c ON c.IdCountry = ct.IdCountry
                ORDER BY t.IdTrip", connection);

            connection.Open();
            using var reader = command.ExecuteReader();
            
            var tripDict = new Dictionary<int, dynamic>();

            while (reader.Read())
            {
                int id = (int)reader["IdTrip"];
                
                if (!tripDict.ContainsKey(id))
                {
                    tripDict[id] = new
                    {
                        IdTrip = id,
                        Name = reader["Name"].ToString(),
                        Description = reader["Description"].ToString(),
                        DateFrom = (DateTime)reader["DateFrom"],
                        DateTo = (DateTime)reader["DateTo"],
                        MaxPeople = (int)reader["MaxPeople"],
                        Countries = new List<string>()
                    };
                }
                
                if (reader["Country"] != DBNull.Value)
                    ((List<string>)tripDict[id].Countries).Add(reader["Country"].ToString());
            }

            // slownik na liste
            trips = tripDict.Values.ToList();
            return Ok(trips);
        }
    }
}

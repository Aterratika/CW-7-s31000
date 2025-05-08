using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using CW7_S31000.Models.DTOs;
using CW7_S31000.Exceptions;

namespace CW7_S31000.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        
        private DateTime ParseIntDate(object value)
        {
            int intDate = Convert.ToInt32(value); 
            int year = intDate / 10000;
            int month = (intDate / 100) % 100;
            int day = intDate % 100;

            return new DateTime(year, month, day);
        }
        
        private int GetTodayAsInt()
        {
            var today = DateTime.Today;
            return today.Year * 10000 + today.Month * 100 + today.Day;
        }



        public ClientsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// [2] Zwraca wszystkie wycieczki, na które zapisany jest klient o podanym ID.
        /// Endpoint: GET /api/clients/{id}/trips
        /// </summary>
        [HttpGet("{id}/trips")]
        public IActionResult GetClientTrips(int id)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                // wszystkie wycieczki powiązane z klientem z client_trip
                var command = new SqlCommand(@"
                    SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                           ct.RegisteredAt, ct.PaymentDate
                    FROM Trip t
                    JOIN Client_Trip ct ON t.IdTrip = ct.IdTrip
                    WHERE ct.IdClient = @id", connection);

                command.Parameters.AddWithValue("@id", id);

                connection.Open();
                using var reader = command.ExecuteReader();

                var trips = new List<object>();

                while (reader.Read())
                {
                    trips.Add(new
                    {
                        IdTrip = (int)reader["IdTrip"],
                        Name = reader["Name"].ToString(),
                        Description = reader["Description"].ToString(),
                        DateFrom = (DateTime)reader["DateFrom"],
                        DateTo = (DateTime)reader["DateTo"],
                        MaxPeople = (int)reader["MaxPeople"],
                        RegisteredAt = ParseIntDate(reader["RegisteredAt"]),
                        PaymentDate = reader["PaymentDate"] == DBNull.Value
                            ? (DateTime?)null
                            : ParseIntDate(reader["PaymentDate"])
                         });
                }

                return Ok(trips);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd pobierania wycieczek klienta: {ex.Message}");
            }
        }

        /// <summary>
        /// [3] Tworzy nowego klienta.
        /// Endpoint: POST /api/clients
        /// </summary>
        [HttpPost]
        public IActionResult AddClient([FromBody] ClientCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

                // wstawiamy klienta do bazy i zwracamy id
                var command = new SqlCommand(@"
                    INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
                    OUTPUT INSERTED.IdClient
                    VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)", connection);

                command.Parameters.AddWithValue("@FirstName", dto.FirstName);
                command.Parameters.AddWithValue("@LastName", dto.LastName);
                command.Parameters.AddWithValue("@Email", dto.Email);
                command.Parameters.AddWithValue("@Telephone", dto.Telephone);
                command.Parameters.AddWithValue("@Pesel", dto.Pesel);

                connection.Open();
                var newId = (int)command.ExecuteScalar();

                return CreatedAtAction(nameof(GetClientTrips), new { id = newId }, new { Id = newId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd podczas dodawania klienta: {ex.Message}");
            }
        }

        /// <summary>
        /// [4] Rejestruje klienta na wycieczkę.
        /// Endpoint: PUT /api/clients/{id}/trips/{tripId}
        /// </summary>
        [HttpPut("{id}/trips/{tripId}")]
    public IActionResult RegisterClientForTrip(int id, int tripId)
    {
        try
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            connection.Open();

            var checkClient = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @id", connection);
            checkClient.Parameters.AddWithValue("@id", id);
            if (checkClient.ExecuteScalar() == null)
                throw new NotFoundException("Klient nie istnieje");

            var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @tripId", connection);
            checkTrip.Parameters.AddWithValue("@tripId", tripId);
            var maxPeopleObj = checkTrip.ExecuteScalar();
            if (maxPeopleObj == null)
                throw new NotFoundException("Wycieczka nie istnieje");

            int maxPeople = (int)maxPeopleObj;

            var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @tripId", connection);
            countCmd.Parameters.AddWithValue("@tripId", tripId);
            int count = (int)countCmd.ExecuteScalar();

            if (count >= maxPeople)
                throw new WrongRequestException("Brak miejsc na wycieczkę");

            var insert = new SqlCommand(@"
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
                VALUES (@id, @tripId, @date)", connection);

            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@tripId", tripId);
            insert.Parameters.AddWithValue("@date", GetTodayAsInt());

            insert.ExecuteNonQuery();

            return Ok("Klient został zapisany na wycieczkę");
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (WrongRequestException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Błąd podczas zapisu na wycieczkę: {ex.Message}");
        }
    }
        
        /// <summary>
        /// [5] Usuwa klienta z wycieczki.
        /// Endpoint: DELETE /api/clients/{id}/trips/{tripId}
        /// </summary>
        [HttpDelete("{id}/trips/{tripId}")]
        public IActionResult UnregisterClientFromTrip(int id, int tripId)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                connection.Open();

                //czy klient zapisany na daną wycieczkę
                var check = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId", connection);
                check.Parameters.AddWithValue("@id", id);
                check.Parameters.AddWithValue("@tripId", tripId);

                if (check.ExecuteScalar() == null)
                    throw new NotFoundException("Klient nie jest zapisany na tę wycieczkę");

                // usuwanie zapisu powiazan z client_trip
                var delete = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @id AND IdTrip = @tripId", connection);
                delete.Parameters.AddWithValue("@id", id);
                delete.Parameters.AddWithValue("@tripId", tripId);
                delete.ExecuteNonQuery();

                return Ok("Rejestracja klienta została usunięta");
            }
            catch (NotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd przy usuwaniu rejestracji: {ex.Message}");
            }
        }
    }
}

using HomeCinemaKC.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HomeCinemaKC.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var client = _httpClientFactory.CreateClient();

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
                "http://localhost:8080/realms/master/protocol/openid-connect/token");

            tokenRequest.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("client_id","admin-cli"),
                new KeyValuePair<string,string>("username","admin"),
                new KeyValuePair<string,string>("password","admin"),
                new KeyValuePair<string,string>("grant_type","password")
            });

            var tokenResponse = await client.SendAsync(tokenRequest);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenDoc = JsonDocument.Parse(tokenJson);

            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var tokenElement))
            {
                ModelState.AddModelError("", "Не вдалося отримати токен адміністратора.");
                return View(model);
            }

            var accessToken = tokenElement.GetString();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var userPayload = new
            {
                username = model.Email,
                email = model.Email,
                enabled = true,
                credentials = new[]
                {
                    new {
                        type = "password",
                        value = model.Password,
                        temporary = false
                    }
                }
            };

            var userContent = new StringContent(
                JsonSerializer.Serialize(userPayload),
                Encoding.UTF8,
                "application/json");

            var createUserResponse = await client.PostAsync(
                "http://localhost:8080/admin/realms/home-cinema/users",
                userContent);

            if (!createUserResponse.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Помилка створення користувача.");
                return View(model);
            }

            var usersResponse = await client.GetAsync(
                $"http://localhost:8080/admin/realms/home-cinema/users?username={model.Email}");
            var usersJson = await usersResponse.Content.ReadAsStringAsync();
            var users = JsonDocument.Parse(usersJson);

            if (users.RootElement.GetArrayLength() == 0)
            {
                ModelState.AddModelError("", "Користувача не знайдено.");
                return View(model);
            }

            var userId = users.RootElement[0].GetProperty("id").GetString();

            var clientsResponse = await client.GetAsync(
                "http://localhost:8080/admin/realms/home-cinema/clients");
            var clientsJson = await clientsResponse.Content.ReadAsStringAsync();
            var clients = JsonDocument.Parse(clientsJson);

            string clientUuid = null;
            foreach (var c in clients.RootElement.EnumerateArray())
            {
                if (c.GetProperty("clientId").GetString() == "homecinema-api") // твій clientId
                {
                    clientUuid = c.GetProperty("id").GetString();
                    break;
                }
            }

            if (clientUuid == null)
            {
                ModelState.AddModelError("", "Не знайдено клієнт homecinema-api.");
                return View(model);
            }

            var roleResponse = await client.GetAsync(
                $"http://localhost:8080/admin/realms/home-cinema/clients/{clientUuid}/roles/user");
            var roleJson = await roleResponse.Content.ReadAsStringAsync();
            var roleDoc = JsonDocument.Parse(roleJson);

            if (!roleDoc.RootElement.TryGetProperty("id", out var roleIdElement))
            {
                ModelState.AddModelError("", "Не знайдено роль 'user'.");
                return View(model);
            }

            var roleId = roleIdElement.GetString();

            var rolePayload = new[]
            {
                new {
                    id = roleId,
                    name = "user"
                }
            };

            var roleContent = new StringContent(
                JsonSerializer.Serialize(rolePayload),
                Encoding.UTF8,
                "application/json");

            var assignRoleResponse = await client.PostAsync(
                $"http://localhost:8080/admin/realms/home-cinema/users/{userId}/role-mappings/clients/{clientUuid}",
                roleContent);

            if (!assignRoleResponse.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Не вдалося призначити роль 'user'.");
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        private async Task<string> GetRoleId(HttpClient client, string realm, string roleName)
        {
            var response = await client.GetAsync(
                $"http://localhost:8080/admin/realms/home-cinema/roles/user");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("id").GetString();
        }
    }
}

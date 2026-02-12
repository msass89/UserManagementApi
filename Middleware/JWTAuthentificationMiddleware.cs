using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Options;

namespace UserManagementApi.Middleware
{
    /* Middleware to authenticate requests using a simple token validation mechanism.
    This middleware checks for the presence of an Authorization header in the incoming HTTP request,
    and validates the token against a predefined list of valid tokens. If the token is missing or invalid,
    the middleware responds with a 401 Unauthorized status code and a JSON error message. */
    /* This Example expects token in Authorization header as "Bearer <token>" 
    Bearer token are used for simple token-based authentication with 
    OAuth 2.0 and other token-based authentication schemes. */
    public class JWTAuthentificationMiddleware
    {
        private readonly RequestDelegate _next;
        private const string AUTHORIZATION_HEADER = "Authorization";
        private const string BEARER_PREFIX = "Bearer ";
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;

        public JWTAuthentificationMiddleware(RequestDelegate next, IOptions<UserManagementApi.Options.JwtOptions> options)
        {
            _next = next;
            _jwtKey = options.Value.Key;
            _jwtIssuer = options.Value.Issuer;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Allow anonymous access to /login endpoint
            if (context.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Check for the presence of the Authorization header and validate the token format.
            var authHeader = context.Request.Headers[AUTHORIZATION_HEADER].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith(BEARER_PREFIX))
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized: Missing or invalid token." });
                await context.Response.WriteAsync(errorJson);
                return;
            }

            // Extract the token from the Authorization header and validate it using JWT validation parameters.
            var token = authHeader.Substring(BEARER_PREFIX.Length).Trim();
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtKey);
            try
            {
                // The ValidateToken method checks the token's signature, issuer, audience, and expiration against the provided parameters.
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtIssuer,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);
            }
            catch (SecurityTokenException)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var errorJson = System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized: Invalid or expired token." });
                await context.Response.WriteAsync(errorJson);
                return;
            }

            await _next(context);
        }
    }
}
# Nexus — Security Model

**Status:** Implementation-Ready
**Last Updated:** 2026-04-04
**Author:** Architecture Team
**Classification:** Internal

---

## IMPORTANT: MVP Assumes Trusted LAN, No Authentication

**For MVP (Phases 1–3), the following apply:**
- No authentication required (assumed trusted LAN environment)
- No TLS for hub-to-spoke communication (LAN-only, no public internet exposure)
- Hub accessible only from devices on the same physical LAN
- Pre-shared tokens for spoke registration are optional (can be simple or omitted for MVP)
- Production deployment with remote access requires authentication and TLS (post-MVP feature)

**Post-MVP Authentication & Remote Access:**
- Google OAuth for hub UI access
- mTLS or JWT for spoke-to-hub authentication
- TLS 1.3 for all communication
- Tailscale, Cloudflare Tunnel, or VPN for remote access
- See Phase 5 and Future Considerations for detailed roadmap

---

## 1. Security Architecture Overview

### 1.1 Design Principles

Nexus is a **single-tenant, self-hosted personal AI orchestration platform**. Unlike enterprise multi-tenant systems, threat modeling is grounded in realistic personal-use scenarios, not enterprise paranoia. The system prioritizes:

1. **Data isolation at the spoke level** — credentials and source code never leave the machine where they live
2. **Outbound-only spoke connections** — no inbound ports exposed on spoke machines
3. **Hub exposure control** — hub runs on private infrastructure (LAN for MVP, with optional VPN/Tailscale for post-MVP remote access)
4. **Credential compartmentalization** — hub has no knowledge of external system credentials (Jira, Git, etc.)
5. **Auditability** — all hub API calls and spoke events are logged for accountability

### 1.2 Trust Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│                     SPOKE MACHINE                           │
│  (Work Laptop / Dev Workstation / Personal Dev Server)    │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Trusted Boundary: Local Storage                      │  │
│  │ • Local git credentials                              │  │
│  │ • Jira API tokens                                    │  │
│  │ • Source code (never leaves machine)                 │  │
│  │ • Project workspace                                  │  │
│  │ • Worker container mounts (isolated)                 │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                 │
│                           ▼                                 │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ Spoke Agent (Daemon)                                 │  │
│  │ • Maintains hub connection (outbound only, LAN)      │  │
│  │ • Filters data before sending to hub                 │  │
│  │ • (MVP: Optional pre-shared token, no TLS)           │  │
│  │ • (Post-MVP: mTLS + TLS 1.3 for remote access)       │  │
│  │ • Enforces data boundary (no source code flows out)  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            │
        │ (MVP: No TLS, LAN-only)                │
        │ (Post-MVP: TLS 1.3 + Tailscale/VPN)  │
   ┌─────────────────────────────────────────────┐
   │                                             │
   ▼                                             ▼
            ┌──────────────────────────────────┐
            │  HUB (Docker Compose on LAN)     │
            │  MVP: No Auth Required           │
            │                                  │
            │ ┌──────────────────────────────┐ │
            │ │ .NET 10 API + SignalR        │ │
            │ │ • (MVP: No auth)             │ │
            │ │ • (Post-MVP: Google OAuth)   │ │
            │ │ • No external credentials    │ │
            │ └──────────────────────────────┘ │
            │ ┌──────────────────────────────┐ │
            │ │ PostgreSQL                   │ │
            │ │ • Audit logs                 │ │
            │ │ • Job history                │ │
            │ │ • Spoke registry             │ │
            │ │ (optional at-rest encryption)│ │
            │ └──────────────────────────────┘ │
            │ ┌──────────────────────────────┐ │
            │ │ Next.js UI                   │ │
            │ │ • (MVP: Local network only)  │ │
            │ │ • (Post-MVP: Tailscale/VPN)  │ │
            │ │ • Single-user, no RBAC       │ │
            │ └──────────────────────────────┘ │
            └──────────────────────────────────┘
```

### 1.3 Data Classification

**Data that flows to the hub (ALLOWED):**
- Job status updates (queued, running, completed, failed)
- Implementation plans (structure, approach, not source code)
- Terminal output (stdout/stderr from workers)
- Project metadata (ticket key, name, summary, status)
- Conversation messages between user and spoke
- Summaries of completed work
- Error logs and warnings
- Audit trail of all hub API calls

**Data that NEVER flows to the hub (BLOCKED):**
- Source code (from repos or project directories)
- Credentials (API keys, tokens, passwords, SSH keys, Git credentials)
- Environment variables containing secrets
- Configuration files with sensitive data
- Private ticket details (if containing secrets)
- Local file paths with sensitive context
- Container registry credentials
- Jira API tokens or other external service credentials

---

## 2. Authentication

### 2.1 Hub UI Authentication

The hub is a **single-user application**. Authentication gates all UI access and API calls.

#### Google OAuth 2.0 Flow (PKCE)

**Flow:**
1. User navigates to hub UI (e.g., `https://nexus.home.local`).
2. Hub redirects to Google OAuth consent screen.
3. User authenticates with Google account.
4. Google returns auth code + PKCE code verifier.
5. Hub backend exchanges code for ID token and access token (PKCE guards against authorization code interception).
6. Hub validates ID token signature, extracts `sub` claim (Google account ID).
7. Hub issues a **JWT signed with HS256** (or RS256 with private key) containing `sub` and `email` claims, with 24-hour TTL.
8. Browser stores JWT in **httpOnly, Secure, SameSite=Strict cookie**.

**Hub OAuth Configuration (.NET 10):**

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.Cookie.Name = "__nexus.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = false;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"];
    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
    options.CallbackPath = "/auth/google/callback";
    options.Scope.Add("email");
    options.Scope.Add("profile");
    options.ClaimActions.MapJsonKey("urn:google:picture", "picture");
    options.Events = new OAuthEvents
    {
        OnTicketReceived = async context =>
        {
            var userEmail = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
            var allowedEmail = builder.Configuration["Auth:AllowedEmail"];

            // Whitelist check: only allow specific Google account
            if (userEmail != allowedEmail)
            {
                context.Fail("Unauthorized");
            }
        }
    };
});
```

**Whitelist Enforcement:**
- Configuration entry: `Auth:AllowedEmail` (e.g., `user@example.com`)
- Checked during OAuth callback in `OnTicketReceived` event
- If Google email doesn't match, authentication fails
- Only one Google account is ever allowed to use the hub

#### JWT Token Management & Refresh Token Flow

**JWT Claims (HS256, 15-minute expiry):**
```json
{
  "sub": "114821549186348652619",  // Google account ID
  "email": "user@example.com",
  "exp": 1712361900,
  "iat": 1712361200,
  "iss": "nexus-hub",
  "aud": "nexus-web"
}
```

**Refresh Token (httpOnly cookie, 7-day lifetime):**
- Stored in database with spoke_id (for spoke sessions)
- Set as httpOnly, Secure, SameSite=Strict cookie
- Expires after 7 days of inactivity
- Revocation: delete from database (immediate effect, user kicked out within 15 min)

**Token Signing:**
```csharp
public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public string GenerateToken(string googleSub, string email)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SigningKey"]);
        var handler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, googleSub),
                new Claim(ClaimTypes.Email, email),
                new Claim("spoke_id", "") // Placeholder; filled for spoke tokens
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),  // 15-minute JWT
            Issuer = "nexus-hub",
            Audience = "nexus-web",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    public async Task<(string jwt, string refreshTokenId)> GenerateTokenWithRefreshAsync(
        string googleSub,
        string email,
        string spokeId = null)
    {
        var jwt = GenerateToken(googleSub, email);

        // Store refresh token in database
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = googleSub,
            SpokeId = string.IsNullOrEmpty(spokeId) ? null : Guid.Parse(spokeId),
            Token = GenerateSecureRandomToken(32),  // 256-bit
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IssuedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return (jwt, refreshToken.Id.ToString());
    }

    public async Task<string> RefreshJwtAsync(string refreshTokenId)
    {
        var refreshToken = await _refreshTokenRepository.GetAsync(Guid.Parse(refreshTokenId));

        if (refreshToken == null || refreshToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Refresh token invalid or expired");
        }

        // Generate new JWT using stored user info
        return GenerateToken(refreshToken.UserId, refreshToken.Email);
    }

    public async Task RevokeSessionAsync(string refreshTokenId)
    {
        await _refreshTokenRepository.DeleteAsync(Guid.Parse(refreshTokenId));
        await _refreshTokenRepository.SaveChangesAsync();
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SigningKey"]);
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = "nexus-hub",
                ValidateAudience = true,
                ValidAudience = "nexus-web",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(5)
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"JWT validation failed: {ex.Message}");
            return null;
        }
    }

    private string GenerateSecureRandomToken(int lengthBytes)
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            var bytes = new byte[lengthBytes];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
```

#### Session Management

- **JWT Cookie:** 15-minute expiry, no sliding window. Expired JWT forces refresh.
- **Refresh Token:** 7-day lifetime in httpOnly cookie. Used to obtain new JWT via `/api/auth/refresh` endpoint.
- **Logout:** Delete refresh token from database. User is kicked out within 15 minutes (current JWT expires).
- **Session Revocation:** Hub can revoke a session immediately by deleting the refresh token. No token blacklist needed.
- **Browser storage:** JWT in httpOnly cookie; refresh token also in httpOnly cookie. Never in localStorage.
- **CSRF protection:** SameSite=Strict cookie prevents CSRF attacks.

**Refresh Endpoint:**
```csharp
[HttpPost("/api/auth/refresh")]
[AllowAnonymous]  // Authenticated via refresh token cookie
public async Task<IActionResult> RefreshToken()
{
    var refreshTokenId = Request.Cookies["nexus.refresh"];
    if (string.IsNullOrEmpty(refreshTokenId))
        return Unauthorized();

    try
    {
        var newJwt = await _jwtService.RefreshJwtAsync(refreshTokenId);
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        Response.Cookies.Append("nexus.auth", newJwt, cookieOptions);
        return Ok(new { message = "Token refreshed" });
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Refresh failed: {ex.Message}");
        return Unauthorized();
    }
}

### 2.2 Spoke Authentication

Spokes use a **two-stage authentication model**: PSK-based initial registration → JWT for subsequent connections.

#### PSK → JWT Exchange Flow

**Initialization (Spoke Setup):**
1. Operator generates PSK: `nexus spoke generate-psk --spoke-id <id>`
2. Hub stores PSK hash (PBKDF2, 10K iterations)
3. Spoke loads PSK from OS keychain or encrypted config

**First Connection (PSK Validation):**
1. Spoke sends PSK in Authorization header: `Authorization: Bearer {psk}`
2. Hub validates PSK against stored hash
3. Hub issues spoke-specific JWT (24-hour lifetime)
4. Spoke receives JWT, caches locally, uses for all subsequent SignalR connections

**Subsequent Connections (JWT-Based):**
1. Spoke uses JWT in Authorization header
2. Hub validates JWT signature, expiry
3. Before JWT expires (e.g., after 20 hours), spoke auto-refreshes using PSK
4. Spoke calls `/api/spokes/refresh-jwt` with PSK in Authorization header
5. Hub returns new JWT; cycle continues

**Benefits:**
- PSK only transmitted once per 24 hours (at refresh time), not on every reconnect
- JWT reduces hub PSK validation overhead
- Spoke can operate offline; reconnects with cached JWT when network returns
- PSK rotation doesn't require spoke restart (rotated at next 24-hour refresh)

#### Pre-Shared Key (PSK) Registration & Hashing

**Key Generation (One-Time, During Spoke Setup):**

```csharp
public class PskGenerator
{
    /// <summary>
    /// Generate a cryptographically secure PSK for a new spoke.
    /// Returns a 32-byte (256-bit) key encoded as base64.
    /// </summary>
    public static string GeneratePsk()
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
```

**Hub-Side: Store PSK (Hashed, Not in Plaintext)**

```csharp
public class SpokeRegistrationService
{
    private readonly ISpokeRepository _repository;
    private readonly ILogger<SpokeRegistrationService> _logger;

    public async Task<Spoke> RegisterNewSpokeAsync(
        string spokeName,
        string providedPsk,
        List<string> capabilities)
    {
        // Hash the PSK using PBKDF2 before storing
        var hashedPsk = HashPsk(providedPsk);

        var spoke = new Spoke
        {
            Id = Guid.NewGuid(),
            Name = spokeName,
            Status = SpokeStatus.Offline,
            HashedPsk = hashedPsk,
            Capabilities = capabilities,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(spoke);
        await _repository.SaveChangesAsync();

        _logger.LogInformation($"Spoke {spokeName} registered with ID {spoke.Id}");
        return spoke;
    }

    private string HashPsk(string psk)
    {
        var saltBytes = new byte[16];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(saltBytes);
        }

        using (var pbkdf2 = new Rfc2898DeriveBytes(psk, saltBytes, 10000, HashAlgorithmName.SHA256))
        {
            var hash = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(saltBytes.Concat(hash).ToArray());
        }
    }

    public bool VerifyPsk(string providedPsk, string hashedPsk)
    {
        var hashBytes = Convert.FromBase64String(hashedPsk);
        var saltBytes = hashBytes.Take(16).ToArray();
        var storedHash = hashBytes.Skip(16).Take(32).ToArray();

        using (var pbkdf2 = new Rfc2898DeriveBytes(providedPsk, saltBytes, 10000, HashAlgorithmName.SHA256))
        {
            var computedHash = pbkdf2.GetBytes(32);
            return storedHash.SequenceEqual(computedHash);
        }
    }
}
```

**Spoke-Side: Store PSK (Encrypted Local Config)**

```yaml
# ~/nexus-spoke/.nexus/config.yaml (encrypted with OS keychain or gpg)
hub:
  url: "https://nexus.home.local"
  psk: "${NEXUS_PSK}"  # Loaded from env var, not stored in plaintext
  spoke_id: "550e8400-e29b-41d4-a716-446655440000"

capabilities:
  - jira
  - git
  - docker
```

Environment variable sourced from:
- **Linux:** System keyring (`secret-tool`, `pass`, or `1password`)
- **Windows:** Windows Credential Manager
- **macOS:** Keychain
- **Fallback:** Encrypted config file with permissions `0600`

**Spoke Connection Authentication (SignalR with JWT):**

```csharp
// Spoke daemon
public class SpokeConnector
{
    private readonly HubConnection _connection;
    private readonly string _psk;
    private readonly Guid _spokeId;
    private string _cachedJwt;
    private DateTime _jwtExpiresAt;

    public async Task ConnectAsync()
    {
        // Load cached JWT if available and not expired
        if (_cachedJwt != null && DateTime.UtcNow < _jwtExpiresAt.AddMinutes(-5))
        {
            await ConnectWithJwtAsync(_cachedJwt);
        }
        else
        {
            // No cached JWT or expired; obtain new one using PSK
            _cachedJwt = await ObtainJwtWithPskAsync();
            _jwtExpiresAt = DateTime.UtcNow.AddHours(24);
            await ConnectWithJwtAsync(_cachedJwt);
        }
    }

    private async Task<string> ObtainJwtWithPskAsync()
    {
        // Initial request using PSK to get JWT
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _psk);

            var response = await client.PostAsync(
                $"{_hubUrl}/api/spokes/authenticate",
                new StringContent("", Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("PSK authentication failed");

            var body = await response.Content.ReadAsAsync<JwtResponse>();
            return body.Token;
        }
    }

    private async Task ConnectWithJwtAsync(string jwt)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{_hubUrl}/api/hub", options =>
            {
                // JWT in header for all subsequent connections
                options.Headers.Add("Authorization", $"Bearer {jwt}");
            })
            .WithAutomaticReconnect(new[] { 1000, 2000, 5000, 10000, 30000 }) // 1s → 30s
            .AddMessagePackProtocol()
            .Build();

        await _connection.StartAsync();

        // Schedule JWT refresh before expiry (after 20 hours, before 24-hour expiry)
        _ = ScheduleJwtRefreshAsync();
    }

    private async Task ScheduleJwtRefreshAsync()
    {
        var refreshAt = _jwtExpiresAt.AddHours(-4);  // Refresh 4 hours before expiry
        var delay = refreshAt - DateTime.UtcNow;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        // Refresh JWT using PSK
        try
        {
            _cachedJwt = await ObtainJwtWithPskAsync();
            _jwtExpiresAt = DateTime.UtcNow.AddHours(24);

            // Reconnect with new JWT
            if (_connection.State != HubConnectionState.Connected)
            {
                await ConnectWithJwtAsync(_cachedJwt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"JWT refresh failed: {ex.Message}");
            // Connection will retry automatically; next reconnect will attempt PSK auth again
        }

        // Schedule next refresh
        _ = ScheduleJwtRefreshAsync();
    }

    private class JwtResponse
    {
        public string Token { get; set; }
    }
}
```

**Hub-Side: JWT Authentication Endpoint & SignalR Validation**

```csharp
// Hub controller for PSK → JWT exchange
[ApiController]
[Route("api/spokes")]
public class SpokeAuthController : ControllerBase
{
    private readonly JwtService _jwtService;
    private readonly ISpokeService _spokeService;
    private readonly ILogger<SpokeAuthController> _logger;

    [HttpPost("authenticate")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthenticateWithPsk()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "Missing PSK" });

        var psk = authHeader.Substring("Bearer ".Length);

        // TODO: Extract spoke_id from request body (sent by spoke)
        var spokeId = Guid.Parse(Request.Query["spoke_id"]);

        var spoke = await _spokeService.GetSpokeAsync(spokeId);
        if (spoke == null || !_spokeService.VerifyPsk(psk, spoke.HashedPsk))
        {
            _logger.LogWarning($"PSK authentication failed for spoke {spokeId}");
            return Unauthorized(new { error = "Invalid PSK" });
        }

        // Generate spoke-specific JWT (24-hour lifetime)
        var jwt = _jwtService.GenerateSpokeToken(spokeId, spoke.Name);

        await _auditService.LogAsync(new AuditEvent
        {
            Action = "SPOKE_JWT_ISSUED",
            SpokeId = spokeId,
            Timestamp = DateTime.UtcNow
        });

        return Ok(new { token = jwt });
    }

    [HttpPost("refresh-jwt")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshSpokeJwt()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { error = "Missing PSK" });

        var psk = authHeader.Substring("Bearer ".Length);
        var spokeId = Guid.Parse(Request.Query["spoke_id"]);

        var spoke = await _spokeService.GetSpokeAsync(spokeId);
        if (spoke == null || !_spokeService.VerifyPsk(psk, spoke.HashedPsk))
        {
            _logger.LogWarning($"JWT refresh failed for spoke {spokeId}");
            return Unauthorized(new { error = "Invalid PSK" });
        }

        var jwt = _jwtService.GenerateSpokeToken(spokeId, spoke.Name);
        return Ok(new { token = jwt });
    }
}

// Updated JwtService with spoke token generation
public class JwtService
{
    public string GenerateSpokeToken(Guid spokeId, string spokeName)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SigningKey"]);
        var handler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("spoke_id", spokeId.ToString()),
                new Claim("spoke_name", spokeName)
            }),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = "nexus-hub",
            Audience = "nexus-spoke",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}

// SignalR Hub authentication (validates JWT on connection)
public class NexusHub : Hub
{
    private readonly JwtService _jwtService;
    private readonly IAuditService _auditService;
    private readonly ILogger<NexusHub> _logger;

    public override async Task OnConnectedAsync()
    {
        var authHeader = Context.GetHttpContext().Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            Context.Abort();
            _logger.LogWarning($"Unauthorized SignalR connection attempt");
            return;
        }

        var jwt = authHeader.Substring("Bearer ".Length);
        var principal = _jwtService.ValidateToken(jwt);

        if (principal == null)
        {
            Context.Abort();
            _logger.LogWarning($"Invalid JWT on SignalR connection");
            return;
        }

        var spokeId = principal.FindFirst("spoke_id")?.Value;
        Context.Items["spoke_id"] = spokeId;

        await base.OnConnectedAsync();
        _logger.LogInformation($"Spoke {spokeId} connected via JWT");
    }
}
```

#### PSK Key Rotation

**Procedure (Quarterly or on Compromise):**

1. Generate new PSK: `nexus spoke generate-psk --spoke-id <id>`
2. Hub stores new PSK hash alongside old PSK hash (grace period: 7 days)
3. Spoke receives new PSK via hub UI (or manual config update)
4. Spoke updates local config and restarts daemon
5. After grace period, hub invalidates old PSK
6. Log all rotations in audit trail

**Implementation:**

```csharp
public class SpokeService
{
    public async Task RotatePskAsync(Guid spokeId)
    {
        var spoke = await _repository.GetAsync(spokeId);
        if (spoke == null) throw new InvalidOperationException($"Spoke {spokeId} not found");

        var newPsk = PskGenerator.GeneratePsk();
        var newHashedPsk = HashPsk(newPsk);

        spoke.HashedPsk = newHashedPsk;
        spoke.PskRotatedAt = DateTime.UtcNow;
        spoke.OldHashedPsk = spoke.HashedPsk; // Keep for grace period

        await _repository.UpdateAsync(spoke);
        await _repository.SaveChangesAsync();

        // Audit log
        await _auditService.LogAsync(new AuditEvent
        {
            Action = "PSK_ROTATED",
            SpokeId = spokeId,
            Timestamp = DateTime.UtcNow,
            Details = new { GracePeriodDays = 7 }
        });

        return newPsk; // Return to operator for insertion into spoke config
    }

    // During verification, accept both current and old PSK (if within grace period)
    public bool VerifyPsk(string providedPsk, Spoke spoke)
    {
        if (VerifyHashedPsk(providedPsk, spoke.HashedPsk))
            return true;

        if (spoke.OldHashedPsk != null &&
            spoke.PskRotatedAt.HasValue &&
            DateTime.UtcNow - spoke.PskRotatedAt.Value < TimeSpan.FromDays(7))
        {
            return VerifyHashedPsk(providedPsk, spoke.OldHashedPsk);
        }

        return false;
    }
}
```

#### Optional: mTLS for Future Hardening

Not required for Phase 1, but documented for future deployment.

**Certificate Generation (On Spoke Registration):**

```bash
# Hub generates a self-signed CA certificate (stored in hub's secret storage)
openssl genrsa -out hub-ca.key 4096
openssl req -new -x509 -days 3650 -key hub-ca.key -out hub-ca.crt -subj "/CN=Nexus-Hub-CA"

# For each spoke, hub generates a certificate signed by CA
openssl genrsa -out spoke-550e8400.key 4096
openssl req -new -key spoke-550e8400.key -out spoke-550e8400.csr \
  -subj "/CN=spoke-550e8400-e29b-41d4-a716-446655440000"
openssl x509 -req -in spoke-550e8400.csr \
  -CA hub-ca.crt -CAkey hub-ca.key -CAcreateserial \
  -out spoke-550e8400.crt -days 365
```

**Spoke-Side mTLS Configuration:**

```csharp
var handler = new HttpClientHandler();
handler.ClientCertificateOptions = ClientCertificateOption.Manual;

// Load client certificate
var certBytes = File.ReadAllBytes("~/.nexus/certs/spoke-550e8400.crt");
var keyBytes = File.ReadAllBytes("~/.nexus/certs/spoke-550e8400.key");
var cert = X509Certificate2.CreateFromPemFile(certBytes, keyBytes);
handler.ClientCertificates.Add(cert);

// Load CA certificate for server validation
var caCertBytes = File.ReadAllBytes("~/.nexus/certs/hub-ca.crt");
var caCert = new X509Certificate2(caCertBytes);
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
{
    chain.ChainPolicy.CustomTrustStore.Add(caCert);
    return chain.Build(cert);
};

var connection = new HubConnectionBuilder()
    .WithUrl($"{_hubUrl}/api/hub", options =>
    {
        options.HttpMessageHandlerFactory = _ => handler;
    })
    .Build();
```

### 2.3 Worker Container Security

Workers are ephemeral Docker containers spun up by the spoke. They have **no network access to the hub** and operate in isolation.

#### Isolation Strategy

```dockerfile
# Dockerfile for worker container (ubuntu base + claude-code-cli)
FROM ubuntu:22.04

# Install minimal dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    git \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install Claude Code CLI
RUN curl -fsSL https://install.claude-code.dev | bash

# Create unprivileged user
RUN useradd -m -u 1000 worker
USER worker

WORKDIR /work

ENTRYPOINT ["claude-code"]
```

#### Container Launch (Spoke-Side)

```csharp
public class WorkerOrchestrator
{
    private readonly IDockerClient _dockerClient;
    private readonly ILogger<WorkerOrchestrator> _logger;

    public async Task<string> LaunchWorkerAsync(
        Guid jobId,
        string repoPath,
        string promptPath,
        Guid spokeId)
    {
        var containerId = $"nexus-worker-{jobId:N}";
        var containerName = $"nexus-worker-{jobId:D}";

        var createContainerParameters = new CreateContainerParameters
        {
            Image = "nexus-worker:latest",
            Name = containerName,
            Hostname = containerName,
            AttachStdout = true,
            AttachStderr = true,
            OpenStdin = true,
            StdinOnce = false,
            Tty = false,

            // Security: run as unprivileged user
            User = "1000:1000",

            // Network isolation: none
            NetworkMode = "none",

            // Resource limits
            HostConfig = new HostConfig
            {
                Memory = 4L * 1024 * 1024 * 1024, // 4GB max
                MemorySwap = 4L * 1024 * 1024 * 1024,
                CpuQuota = 100000, // 1 CPU
                CpuPeriod = 100000,

                // Read-only root filesystem (except /tmp)
                ReadonlyRootfs = true,

                // Mounts: only repo (ro) and prompt (ro)
                Binds = new List<string>
                {
                    $"{repoPath}:/work/repo:ro",
                    $"{promptPath}:/work/prompt.md:ro",
                    "/tmp:/tmp"  // Writable tmp for scratch
                },

                // Drop capabilities
                CapAdd = new List<string>(),
                CapDrop = new List<string> { "ALL" },

                // No privileged or escalation
                Privileged = false,
                NoNewPrivileges = true
            },

            // Environment: minimal
            Env = new List<string>
            {
                "CLAUDE_CODE_PROMPT=/work/prompt.md",
                "CLAUDE_CODE_OUTPUT=/tmp/output.json",
                "HOME=/tmp",
                "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
            }
        };

        try
        {
            var response = await _dockerClient.Containers.CreateContainerAsync(createContainerParameters);
            _logger.LogInformation($"Worker container {containerName} created: {response.ID}");

            // Start the container
            var started = await _dockerClient.Containers.StartContainerAsync(
                response.ID,
                new ContainerStartParameters());

            if (!started)
            {
                _logger.LogError($"Failed to start worker container {containerName}");
                throw new InvalidOperationException($"Worker container failed to start");
            }

            return response.ID;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Worker launch failed: {ex.Message}");
            throw;
        }
    }

    public async Task CleanupWorkerAsync(string containerId, Guid jobId)
    {
        try
        {
            // Stop (graceful, 30s timeout)
            await _dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 30 });

            // Remove
            await _dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters { Force = true });

            _logger.LogInformation($"Worker container {containerId} cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Worker cleanup failed: {ex.Message}");
        }
    }
}
```

#### Offline Spoke Support: Pending Commands Queue

**Architecture:** When a spoke is offline, the hub queues commands in a `pending_commands` table. On reconnect, the hub sends undelivered commands to the spoke in order.

**Database Schema:**
```sql
CREATE TABLE "PendingCommands" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "SpokeId" UUID NOT NULL,
    "CommandType" VARCHAR(50) NOT NULL,        -- "job_assignment", "approval", "revoke", etc.
    "Payload" JSONB NOT NULL,                  -- Command details
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "DeliveredAt" TIMESTAMP WITH TIME ZONE,    -- NULL if not yet delivered
    "AcknowledgedAt" TIMESTAMP WITH TIME ZONE, -- NULL if not yet acknowledged by spoke
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL, -- 24-hour TTL
    CONSTRAINT fk_spoke FOREIGN KEY ("SpokeId") REFERENCES "Spokes"("Id")
);
CREATE INDEX idx_pending_spoke ON "PendingCommands"("SpokeId", "DeliveredAt") WHERE "DeliveredAt" IS NULL;
```

**Spoke Reconnect Flow:**
1. Spoke connects to hub with JWT
2. Hub queries undelivered commands: `WHERE SpokeId = ? AND DeliveredAt IS NULL AND ExpiresAt > NOW()`
3. Hub sends commands in order (sorted by CreatedAt)
4. Spoke receives each command, executes, sends acknowledgment with command ID
5. Hub marks command as AcknowledgedAt
6. If command expired before delivery, hub:
   - Deletes command from DB
   - Sends notification to user: "Job X expired while offline, cancelled"
   - Logs audit event: `COMMAND_EXPIRED`

**Implementation:**
```csharp
public class PendingCommandService
{
    private readonly IPendingCommandRepository _repository;
    private readonly IHubContext<NexusHub> _hubContext;
    private readonly IAuditService _auditService;

    public async Task<List<PendingCommand>> GetUndeliveredCommandsAsync(Guid spokeId)
    {
        return await _repository.QueryAsync(c =>
            c.SpokeId == spokeId &&
            c.DeliveredAt == null &&
            c.ExpiresAt > DateTime.UtcNow);
    }

    public async Task QueueCommandAsync(Guid spokeId, string commandType, object payload)
    {
        var command = new PendingCommand
        {
            Id = Guid.NewGuid(),
            SpokeId = spokeId,
            CommandType = commandType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        await _repository.AddAsync(command);
        await _repository.SaveChangesAsync();
    }

    public async Task MarkDeliveredAsync(Guid commandId)
    {
        var command = await _repository.GetAsync(commandId);
        command.DeliveredAt = DateTime.UtcNow;
        await _repository.UpdateAsync(command);
        await _repository.SaveChangesAsync();
    }

    public async Task MarkAcknowledgedAsync(Guid commandId)
    {
        var command = await _repository.GetAsync(commandId);
        command.AcknowledgedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(command);
        await _repository.SaveChangesAsync();
    }

    public async Task CleanupExpiredCommandsAsync()
    {
        var expired = await _repository.QueryAsync(c => c.ExpiresAt <= DateTime.UtcNow);

        foreach (var cmd in expired)
        {
            // Notify user if command is a job assignment
            if (cmd.CommandType == "job_assignment")
            {
                var jobId = JsonSerializer.Deserialize<JsonElement>(cmd.Payload)
                    .GetProperty("job_id").GetString();

                await _auditService.LogAsync(new AuditEvent
                {
                    Action = "COMMAND_EXPIRED",
                    SpokeId = cmd.SpokeId,
                    Details = new { JobId = jobId, CommandId = cmd.Id },
                    Timestamp = DateTime.UtcNow
                });
            }

            await _repository.DeleteAsync(cmd.Id);
        }

        await _repository.SaveChangesAsync();
    }
}

// Scheduled cleanup job (run hourly)
public class ExpiredCommandCleanupJob : IJob
{
    private readonly PendingCommandService _service;

    public async Task Execute(IJobExecutionContext context)
    {
        await _service.CleanupExpiredCommandsAsync();
    }
}

// On spoke reconnect (SignalR OnConnectedAsync)
public override async Task OnConnectedAsync()
{
    // ... JWT validation ...

    var spokeId = Guid.Parse(Context.Items["spoke_id"] as string);

    // Get undelivered commands
    var commands = await _pendingCommandService.GetUndeliveredCommandsAsync(spokeId);

    foreach (var cmd in commands)
    {
        // Send to spoke
        await Clients.Client(Context.ConnectionId)
            .SendAsync("command.pending", new
            {
                CommandId = cmd.Id,
                CommandType = cmd.CommandType,
                Payload = cmd.Payload
            });

        // Mark as delivered
        await _pendingCommandService.MarkDeliveredAsync(cmd.Id);
    }

    await base.OnConnectedAsync();
}

// Spoke acknowledges command
public async Task AcknowledgeCommandAsync(Guid commandId)
{
    await _pendingCommandService.MarkAcknowledgedAsync(commandId);
}
```

#### Credential Isolation

**Workers receive scoped git and GitHub credentials** to enable the ticket-to-merge pipeline (commit, push, create PRs). This is a deliberate tradeoff: direct worker access to git/GitHub is required for autonomous operation, while sandboxing limits blast radius.

**What workers receive (read-only, scoped):**
1. **Git identity** — `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME`, `GIT_COMMITTER_EMAIL` env vars
2. **Git auth** — Either SSH key mounted read-only at `/tmp/.ssh/id_key`, or `GIT_TOKEN` env var for HTTPS credential helper
3. **GitHub CLI auth** — `GH_TOKEN` env var (for `gh pr create`, etc.)
4. **Anthropic API key** — `ANTHROPIC_API_KEY` env var (for Claude Code CLI)
5. **Read-write mounted repo** (cloned locally on spoke)
6. **Prompt file** (injected by spoke, read-only)

**What workers do NOT receive:**
- Jira API tokens or other external service credentials
- Spoke-to-hub authentication tokens
- Container registry credentials
- Any credentials beyond git/GitHub scope

**Security controls retained:**
- `CapDrop: ALL` — all Linux capabilities dropped
- `SecurityOpt: no-new-privileges` — prevents privilege escalation
- Unprivileged user (UID 1000:1000) — no root access
- Resource limits (memory, CPU, disk) — prevents resource exhaustion
- Ephemeral containers — destroyed after job completes, no persistent state
- SSH keys mounted read-only — worker cannot modify or exfiltrate to persistent storage

**Network access:** Workers use `NetworkMode: bridge` by default, inheriting the spoke machine's network access. This enables `git push`, `gh pr create`, and Anthropic API calls. Operators can set `NetworkMode: none` for analysis-only spokes where workers should not have network access.

**Configuration:** Credentials are configured in the spoke's `config.yaml` under `Docker.Credentials`. See spoke configuration documentation for details.

---


## 2.4 PR Monitoring Credentials

PR monitoring requires local Git platform credentials (GitHub tokens, Azure DevOps Personal Access Tokens) to interact with Pull Request APIs. These credentials are handled with strict compartmentalization.

> **Note:** Workers also use the same `GH_TOKEN` / git credentials (configured under `Docker.Credentials` in `config.yaml`) for direct PR creation during job execution. The spoke's PR monitoring service and worker containers share the same credential source.

### Storage

- **GitHub Tokens:** Stored in spoke config (`config.yaml`) under `git.credentials.github_token` or via environment variable `NEXUS_GITHUB_TOKEN`.
- **Azure DevOps PATs:** Stored similarly under `git.credentials.azure_devops_pat` or via `NEXUS_AZURE_PAT`.
- **Storage Location:** Local spoke filesystem only, never persisted in the hub database.
- **Encryption:** Credentials stored in plaintext in config (relying on OS file permissions and optional LUKS encryption on the spoke machine). Alternative: use credential managers (e.g., `git credential-osxkeychain` on macOS, `git-credential-manager` on Linux).

### Scope & Lifetime

- **Scope:** Minimal—GitHub token requires only `repo:read_only` + `pull_request:read` scopes. Azure DevOps PAT scoped to `Code (read)`.
- **Lifetime:** No expiration enforced by Nexus; relies on organization's token management policy. Tokens should be rotated periodically and revoked if compromised.

### Data Flow (PR Monitoring → Hub)

```
┌─────────────────────────────────────────────────────────────┐
│                    SPOKE MACHINE                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ PR Monitoring Service                                │   │
│  │ • Uses local GitHub/Azure credentials               │   │
│  │ • Fetches PRs, comments, files from Git platform    │   │
│  │ • Extracts comment text, code context               │   │
│  │ • Classifies comment via Claude API                 │   │
│  │ • Creates fix jobs, posts responses (locally)       │   │
│  └──────────────────────────────────────────────────────┘   │
│                     ↓                                         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ Data Boundary: Only classified metadata flows out    │   │
│  │ ✓ Classification (actionable, invalid, positive)    │   │
│  │ ✓ Comment text (for user review)                    │   │
│  │ ✓ PR metadata (number, title, branch)               │   │
│  │ ✓ Job creation events                               │   │
│  │ ✗ GitHub/Azure credentials (never sent)             │   │
│  │ ✗ Full PR diff or file contents (only snippets)    │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
          │ TLS 1.3, outbound-only WebSocket
          ▼
        ┌──────────┐
        │   HUB    │
        │          │
        │ Records: │
        │ • PR comment detected event
        │ • Fix job created
        │ • User query: list my monitored PRs
        │ (NOT: credentials, file diffs, secrets)
        └──────────┘
```

### Responding to Comments (Via PendingActions)

**No auto-posting to PRs.** All proposed responses are routed through PendingActions on the hub for user review before posting.

**Flow:**
1. Spoke detects PR comment, classifies it (actionable, positive, invalid, etc.)
2. Spoke proposes a response (if applicable) and sends to hub as `PendingAction`
3. Hub stores pending action with:
   - PR metadata (repo, PR number, comment ID)
   - Proposed response text
   - Classification (for context)
   - Status: pending
4. User reviews pending action in hub UI
5. User approves or modifies response
6. Hub sends approval back to spoke
7. Spoke posts response using local GitHub/Azure credentials
8. Spoke notifies hub: response posted, marks PendingAction as completed

**Duplicate Prevention:**
- Track processed comments by external comment ID (GitHub ID, Azure ID)
- Before posting response, check if this comment ID already has a processed action
- Skip if already handled; log as duplicate

**Benefits:**
- User always sees what will be posted before it happens
- No surprise PR comments posted by automation
- Credentials never on hub; spoke handles all posting
- Audit trail shows user approval before response posted

### Audit Trail

- Spoke logs all PR monitoring activities locally: `~/.nexus/logs/pr-monitoring.log`.
- Hub records PR-related events (PrCommentDetected, PrFixJobCreated, PrCommentResolved) for audit purposes.
- User can query hub for PR monitoring history and links to spoke logs for detailed audit.

### Risk Mitigation

- **Credential Compromise:** If GitHub token leaked, attacker can only read PRs and comment on them (read-only scope). Spoke machine must revoke token via GitHub settings immediately.
- **Spoke Machine Compromise:** If spoke is compromised, attacker gains access to all local credentials. This is a spoke-level risk (same as accessing Git repos, Jira tokens, SSH keys). Nexus adds no additional exposure.
- **Man-in-the-Middle:** All spoke ↔ hub communication is TLS 1.3. Git platform APIs are over HTTPS. No credentials travel over the hub connection.

---

## 2.5 Claude Code Process Security & Conversations

Conversations are multi-turn sessions with Claude Code instances. Spokes invoke CC on a per-message basis (using `claude-code --resume` to maintain session state). Hub runs its own CC meta-agent for cross-system queries.

### CC Process Invocation (Spoke)

**Security:**
- Spoke daemon runs CC as a subprocess, **NOT exposed to network**.
- Each conversation has a `ccSessionId` that maps to a local CC session directory on the spoke.
- CC process runs with spoke user permissions (non-privileged).
- Input: conversation messages from hub (plain text) + local project context (injected by spoke).
- Output: CC response streamed back to spoke daemon, which mirrors to hub database.
- **CC has access to:** Local project files, git config, Jira credentials (on spoke machine). **CC does NOT have access to:** Hub data, other spokes, external internet (except Claude API).

**Session Isolation:**
```
Spoke Machine
├─ Conversation 1 ─→ CC Session (project-A context)
├─ Conversation 2 ─→ CC Session (project-B context)
└─ Hub Conversation ─→ (No local project context, cross-system reasoning)
```

Each conversation's CC session is isolated in `~/.nexus/sessions/{conversationId}/`.

### Conversation Data Storage (Hub)

**Hub Database:**
- Conversations: stored as records with `spokeId` (nullable), `title`, `createdAt`, `updatedAt`, `ccSessionId`
- ConversationMessages: `conversationId`, `role` (user/assistant), `content`, `timestamp`
- **Encryption:** Single-tenant, same security posture as other hub data. Database encryption at rest (if configured on hub machine).

**Data Allowed in Conversations:**
- ✓ User prompts and questions
- ✓ Claude Code responses and explanations
- ✓ Job summaries and implementation plans
- ✓ Code snippets and architecture discussion (up to 1000 lines, contextual)
- ✓ Error messages and diagnostic information
- ✗ Full source code (excluded by spoke before sending)
- ✗ Credentials, API keys, secrets (excluded by spoke)
- ✗ Sensitive business logic (user's responsibility to filter)

**Spoke Filtering:**
Before sending conversation content to hub, spoke daemon sanitizes:
- Removes file paths that reveal project structure (replace with `<repo>/path/to/file`)
- Removes actual credential references (replace with `[CREDENTIAL]`)
- Truncates large code blocks to first/last 20 lines + ellipsis
- Logs sanitization events for audit

### Worker & Spoke MCP Access

**CRITICAL ARCHITECTURE POINT:** Workers do not access MCPs. Spokes access MCPs locally.

**Workers (Containers):**
- Ephemeral Docker containers spun up by the spoke daemon
- Receive minimal environment: repo (read-only), prompt file (read-only), /tmp (write)
- **Do NOT receive MCP config or access to MCPs**
- Output code changes to `/tmp/output.json`; spoke applies changes to repo
- Cannot interact with Jira, Git, or other external systems directly

**Spokes (Local):**
- Receive MCP config from spoke daemon at startup
- Config (`spoke-mcp-config.json`) points to local MCPs: Jira, Git, file system
- MCP connections stay local to the spoke machine
- Spoke's CC instance uses MCPs when needed: `cc --mcp-config ./spoke-mcp-config.json`
- Examples: Query Jira for ticket details, read git history, access local files

**Hub-Local Tools (NOT MCP connections):**
- Hub CC uses hub-local tools to query spokes (e.g., `query_spoke()`, `list_spokes()`)
- These tools send SignalR messages over the encrypted WebSocket; they are NOT MCP connections
- Spoke receives query message, optionally invokes its own CC with MCPs, responds via SignalR
- Hub never connects to or invokes spoke MCPs directly

**Hub → Spoke Query Pattern (Via SignalR, NOT MCP):**
```
Hub CC Instance
    ↓
Invokes hub-local tool: query_spoke(spoke_id, question)
    ↓
Hub-local tool sends SignalR message: spoke.query
    ↓ (authenticated, TLS-encrypted SignalR WebSocket)
Spoke receives message on SignalR listener
    ↓
Spoke optionally invokes local CC with spoke-local MCPs
    ↓
Spoke sends response via SignalR: spoke.query_response
    ↓
Hub-local tool receives response, returns to Hub CC
    ↓
Hub CC processes response
```

**Key Points:**
- Workers: No MCPs, no external access, output-only
- Spokes: Local MCPs via config, accessed by spoke's CC instance
- Hub: Never sees or interacts with spoke MCPs; queries via SignalR only
- All external system credentials (Jira, Git) stay on spoke; hub never sees them

---

## 3. Authorization

### 3.1 Hub Authorization Model

No RBAC needed (single user), but **all API endpoints are gated by authentication**.

**Principle:** If user is authenticated (valid JWT), they can perform any action. No granular permissions.

**Implementation:**

```csharp
// Require authentication on all controllers
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class ProjectController : ControllerBase
{
    [HttpGet("{projectId}")]
    [Authorize]  // Explicit, for clarity
    public async Task<ActionResult<ProjectDto>> GetProject(Guid projectId)
    {
        var project = await _projectService.GetProjectAsync(projectId);
        if (project == null) return NotFound();
        return Ok(project);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectRequest request)
    {
        var project = await _projectService.CreateProjectAsync(request);
        return CreatedAtAction(nameof(GetProject), new { projectId = project.Id }, project);
    }
}
```

**Unauthenticated Endpoints (Public/No Auth):**
- `GET /health` — Health check (no auth required)
- `POST /api/auth/google` — OAuth callback (no auth required, identity confirmed by Google)
- All others: `[Authorize]` mandatory

### 3.2 Spoke-Level Permissions

Each spoke can be configured with **approval gates** for certain job types.

**Example Configuration:**

```yaml
# Spoke config
spoke:
  id: "550e8400-e29b-41d4-a716-446655440000"
  name: "Work Laptop"

approval_gates:
  # Require approval before executing these job types
  required_for:
    - implement       # Implementation jobs need approval before starting
    - refactor
    - deploy-related

  # Auto-approve low-risk jobs
  auto_approve_for:
    - investigate
    - test
    - documentation

  # Approval timeout: if user doesn't approve in 24h, job is cancelled
  approval_timeout_hours: 24
```

**Implementation:**

```csharp
public class JobApprovalService
{
    public async Task<bool> RequiresApprovalAsync(Guid jobId)
    {
        var job = await _jobRepository.GetAsync(jobId);
        var spoke = await _spokeRepository.GetAsync(job.SpokeId);

        var spokeConfig = JsonSerializer.Deserialize<SpokeConfig>(spoke.Config);
        return spokeConfig.ApprovalGates.RequiredFor.Contains(job.Type.ToString());
    }

    public async Task ApproveJobAsync(Guid jobId, string userId)
    {
        var job = await _jobRepository.GetAsync(jobId);
        job.Status = JobStatus.Approved;
        job.ApprovedAt = DateTime.UtcNow;
        job.ApprovedBy = userId;

        await _jobRepository.UpdateAsync(job);

        // Notify spoke that job is approved
        await _hubContext.Clients
            .Group($"spoke-{job.SpokeId}")
            .SendAsync("job.approved", job.Id);

        await _auditService.LogAsync(new AuditEvent
        {
            Action = "JOB_APPROVED",
            JobId = jobId,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

---

## 4. Network Security

### 4.1 Hub Network Exposure

The hub is **NOT on the public internet**. It is exposed via one of:

#### Option A: Tailscale Mesh (Recommended for Personal Use)

- Tailscale client on machine hosting hub
- Tailscale clients on spoke machines (work laptop, dev server, etc.)
- All machines on same Tailnet (private mesh VPN)
- Hub listens on Tailscale IP (e.g., `100.123.45.67:5000`)
- Mobile access: Tailscale app on phone, no public DNS required

**Benefits:**
- Zero-trust: encrypted, device-authenticated, peer-to-peer
- NAT traversal: spoke behind network firewall? No problem
- No ingress rules required on spoke machines
- Mobile-friendly: Tailscale app on iOS/Android

**Setup:**

```bash
# On hub machine (self-hosted Kubernetes)
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up --authkey $TS_AUTHKEY

# Verify Tailscale IP
tailscale ip -4  # e.g., 100.123.45.67

# Configure hub to listen on Tailscale interface
# appsettings.json or k8s ingress
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://100.123.45.67:5000"
      },
      "Https": {
        "Url": "https://100.123.45.67:5001",
        "Certificate": {
          "Path": "/etc/certs/server.crt",
          "KeyPath": "/etc/certs/server.key"
        }
      }
    }
  }
}
```

#### Option B: Cloudflare Tunnel (For Hybrid Access)

- Cloudflare Tunnel daemon on hub machine
- Hub exposed via `nexus.example.com` (Cloudflare DNS)
- Cloudflare handles DDoS protection, TLS termination
- Cloudflare access policy gates to single user

**Benefits:**
- Professional DNS name
- Cloudflare DDoS protection
- Can combine with Tailscale for defense-in-depth

**Setup:**

```bash
# Install Cloudflare tunnel daemon
curl -L --output cloudflared.tgz https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.tgz
tar -xzf cloudflared.tgz

# Authenticate and route
sudo ./cloudflared tunnel create nexus
sudo ./cloudflared tunnel route dns nexus nexus.example.com
sudo ./cloudflared tunnel run nexus

# In cloudflared config
[config.yaml]
tunnel: <tunnel-id>
credentials-file: ~/.cloudflared/cert.pem

ingress:
  - hostname: nexus.example.com
    service: http://localhost:5000
  - service: http_status:404

# Cloudflare Access policy (optional)
# https://dash.cloudflare.com → Access → Add an application
# - Allowed emails: user@example.com only
# - SAML or OTP authentication
```

#### Option C: VPN (Traditional)

- Hub accessible only via corporate VPN or personal VPN server
- Least recommended for personal use; adds operational complexity

### 4.2 Spoke ↔ Hub Communication

**All traffic over TLS 1.3 (encrypted in transit):**

```csharp
// Spoke → Hub (outbound only, no inbound firewall rules needed)
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
{
    // If using self-signed cert, custom validation
    // if (cert.Thumbprint == expectedThumbprint) return true;
    // Otherwise, use default Windows/system cert validation
    return errors == System.Net.Security.SslPolicyErrors.None;
};

var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://nexus.home.local:5001")
};
```

**Hub Certificate (Self-Signed or CA-Signed):**

```bash
# Self-signed (for private infrastructure)
openssl req -x509 -newkey rsa:4096 -nodes -out cert.pem -keyout key.pem -days 365 \
  -subj "/CN=nexus.home.local"

# In k3s/docker-compose
volumes:
  - /etc/nexus/certs/cert.pem:/etc/certs/server.crt:ro
  - /etc/nexus/certs/key.pem:/etc/certs/server.key:ro
```

### 4.3 Spoke Local Network Access

Spokes access local resources (Jira, Git) on their local network. **The hub does NOT** tunnel this traffic.

**Architecture:**
```
Spoke (Work Laptop)
  └─ Local Jira (http://jira.internal:8080)
     └ Spoke queries Jira API directly
  └─ Local Git (git.internal)
     └ Spoke clones/pushes directly
  └─ Hub connection (outbound only, encrypted)
     └ Hub receives status/output, never sends data to local resources
```

**This is intentional:**
- Credentials for local Jira/Git never transmitted to hub
- Hub does not need knowledge of internal network topology
- Spoke acts as a firewall/proxy boundary

---

## 5. Data Security

### 5.1 Data Boundary Architecture

The data boundary is **architectural, not filter-based**. The hub simply does not display full code diffs or source code to the user. Credential isolation is handled at the spoke level (credentials never leave the spoke machine).

**Principle:** Credentials and source code stay on the spoke; hub displays job status, output summaries, and conversation context only.

**Data Allowed in Hub → UI:**
- Job ID, project ID, spoke ID
- Job status (queued, running, completed, failed, cancelled)
- Job summary (high-level text description, not full output)
- Implementation plans (structure and approach, not source code)
- Terminal output snippets (stdout/stderr summaries for context)
- Project name, ticket key, status
- Conversation messages between user and spoke
- Error messages and diagnostic information
- Spoke heartbeat and resource usage (CPU %, memory %)
- PR metadata (PR number, title, branch), comment text for review

**Data NEVER Transmitted from Spoke to Hub:**
- Source code files or diffs
- Credentials (API keys, tokens, passwords, SSH keys)
- Configuration files with secrets
- Full file contents or internal paths
- Sensitive environment variables
- Full git diffs or patch files

**How This Works:**
- Worker outputs go to spoke, not to hub
- Spoke reads output, extracts summaries/plans, sends only safe content to hub
- Hub UI shows conversation and status, not raw code
- User can view full output on spoke machine if needed
- Credentials stored and used only on spoke; hub never sees them

This eliminates the need for heuristics or content filtering; the architecture itself enforces the boundary.

### 5.2 Data at Rest

#### Hub Database (PostgreSQL)

**Encryption at Rest:**
- PostgreSQL Transparent Data Encryption (if available, enterprise edition)
- **OR** OS-level encryption (Linux: LUKS, macOS: FileVault, Windows: BitLocker)

**Setup on Self-Hosted Infrastructure:**
```bash
# Create encrypted LVM volume
sudo lvcreate -L 100G -n nexus_data vg0
sudo cryptsetup luksFormat /dev/vg0/nexus_data
sudo cryptsetup luksOpen /dev/vg0/nexus_data nexus_data_decrypted
sudo mkfs.ext4 /dev/mapper/nexus_data_decrypted
sudo mkdir /var/lib/nexus-db
sudo mount /dev/mapper/nexus_data_decrypted /var/lib/nexus-db
sudo chown postgres:postgres /var/lib/nexus-db
sudo chmod 700 /var/lib/nexus-db

# PostgreSQL data directory
sudo -u postgres initdb -D /var/lib/nexus-db/data
```

**PostgreSQL Configuration (`postgresql.conf`):**
```
# Force SSL on all connections
ssl = on
ssl_cert_file = '/etc/postgresql/server.crt'
ssl_key_file = '/etc/postgresql/server.key'

# Restrict connections to localhost (spoke connections are outbound, so no direct DB access)
listen_addresses = 'localhost'
```

**Database Access Control:**
```sql
-- Only the nexus-api user can connect
CREATE ROLE nexus_api LOGIN PASSWORD '<strong_password>';
GRANT ALL PRIVILEGES ON DATABASE nexus TO nexus_api;
GRANT USAGE, CREATE ON SCHEMA public TO nexus_api;

-- Revoke public defaults
REVOKE ALL ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON DATABASE nexus FROM PUBLIC;
```

#### Spoke Workspace (Local Disk)

**Local Encryption (Recommended):**
- **Linux:** LUKS or `ecryptfs`
- **macOS:** FileVault (already enabled on most machines)
- **Windows:** BitLocker or Windows Defender Encryption

**PSK Storage (Spoke):**
```bash
# Never in plaintext
# ~/.nexus/config.yaml should be unreadable except to the user
chmod 600 ~/.nexus/config.yaml

# PSK loaded from OS keychain/credentials manager
# Example: systemd secret service or pass utility
pass show nexus/hub-psk
```

**Spoke Workspace Permissions:**
```bash
chmod 700 ~/.nexus/                    # Owner only
chmod 700 ~/.nexus/memories/           # Owner only
chmod 700 ~/.nexus/projects/           # Owner only
chmod 700 ~/.nexus/.nexus/secrets/     # Owner only, if exists
```

### 5.3 Data in Transit

**TLS 1.3 for all hub ↔ spoke communication:**

```csharp
// Spoke
var connection = new HubConnectionBuilder()
    .WithUrl($"https://nexus.home.local/api/hub", options =>
    {
        options.DefaultHttpVersion = System.Net.HttpVersion.Version20;
        // TLS 1.3 negotiation handled by OS
    })
    .WithAutomaticReconnect()
    .Build();

// Hub (.NET 10 Kestrel)
var builder = WebApplicationBuilder.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.Listen(IPAddress.Loopback, 5001, listenOptions =>
    {
        listenOptions.UseHttps("/etc/certs/server.crt", "/etc/certs/server.key",
            httpsOptions =>
            {
                // Force TLS 1.3
                httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls13;
            });
    });
});
```

**Certificate Pinning (Optional, Paranoia Mode):**

For extra defense against MITM (relevant if hub is accessed via Cloudflare tunnel), pin cert thumbprint on spoke:

```csharp
var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
{
    var expectedThumbprint = "1234567890ABCDEF...";  // Hub cert thumbprint
    if (cert.Thumbprint.Equals(expectedThumbprint, StringComparison.OrdinalIgnoreCase))
        return true;

    _logger.LogError("Certificate pinning validation failed");
    return false;
};
```

---

## 6. Credential Management

### 6.1 Hub Credentials

**Secrets Stored (Never in Source Control):**
- Google OAuth client ID and secret
- PostgreSQL connection string (with password)
- JWT signing key (256-bit)
- Tailscale auth key (if using Tailscale)
- Cloudflare API token (if using Cloudflare Tunnel)
- TLS certificate private key

**Storage Method:**

```bash
# Kubernetes secrets (if running on k3s)
kubectl create secret generic nexus-config \
  --from-literal=Google__ClientId=<id> \
  --from-literal=Google__ClientSecret=<secret> \
  --from-literal=Jwt__SigningKey=<key> \
  --from-literal=ConnectionStrings__DefaultConnection="Host=...;Password=..."

# Or Docker Compose: .env file (not in git)
# .env (added to .gitignore)
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
JWT_SIGNING_KEY=...
```

**Loading in .NET:**

```csharp
builder.Configuration
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Production.json", optional: true)
    .AddUserSecrets<Program>(optional: true);  // For local dev

// Access
var googleClientId = builder.Configuration["Google:ClientId"];
```

### 6.2 Spoke Credentials

**Secrets Stored (Never in Source Control):**
- Hub PSK
- Jira API token
- Git credentials (SSH key or token)
- Any other external service credentials (GitHub, Azure DevOps, etc.)

**Storage Methods:**

```bash
# Option 1: OS Keychain (Linux: pass, macOS: Keychain, Windows: Credential Manager)
pass show nexus/hub-psk
# Returns: gK9x7mZpQw...

# Option 2: Encrypted config file + environment variable
# ~/.nexus/config.yaml is encrypted via gpg
gpg --decrypt ~/.nexus/config.yaml.gpg > ~/.nexus/config.yaml

# Option 3: Secrets from file, read-protected
cat ~/.nexus/secrets/jira-token  # chmod 600
```

**Example Spoke Startup:**

```csharp
public class SpokeConfiguration
{
    public static async Task<SpokeConfig> LoadAsync()
    {
        // Load hub PSK from keychain
        var psk = await GetFromKeychainAsync("nexus/hub-psk");

        // Load Jira token from encrypted config or keychain
        var jiraToken = await GetFromKeychainAsync("nexus/jira-token");

        // Load Git credentials (via git credential helper)
        var gitCred = await GetGitCredentialsAsync();

        return new SpokeConfig
        {
            HubUrl = Environment.GetEnvironmentVariable("NEXUS_HUB_URL"),
            HubPsk = psk,
            JiraToken = jiraToken,
            GitUsername = gitCred.Username,
            GitPassword = gitCred.Password,
            // ... other config
        };
    }

    private static async Task<string> GetFromKeychainAsync(string key)
    {
        // Platform-specific: Linux (secret-tool), macOS (security), Windows (cmdkey)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var result = await ExecuteCommandAsync("secret-tool", $"lookup password {key}");
            return result.TrimEnd('\n');
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var result = await ExecuteCommandAsync("security", $"find-generic-password -w -a {key} -s nexus");
            return result.TrimEnd('\n');
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use Windows Credential Manager API
            // Not implemented here; use Newtonsoft.Json + Windows API P/Invoke
        }
        throw new NotSupportedException($"Keychain not supported on {RuntimeInformation.OSDescription}");
    }
}
```

### 6.3 Credential Rotation Schedule

- **Hub OAuth secret:** Every 6 months (or if compromised)
- **Spoke PSK:** Every 3 months (or if suspected compromise)
- **JWT signing key:** Every 12 months (or on major security incident)
- **PostgreSQL password:** Every 6 months
- **External service tokens (Jira, Git):** Annually or per vendor policy

**Automation Example:**

```bash
#!/bin/bash
# rotate-secrets.sh (run via cron quarterly)

echo "Rotating Spoke PSKs..."
for spoke_id in $(./nexus list-spokes --json | jq -r '.[].id'); do
    echo "  Rotating $spoke_id..."
    ./nexus spoke rotate-psk --spoke-id=$spoke_id
done

echo "Rotating JWT signing key..."
./nexus rotate-jwt-key

echo "Audit log updated. Review changes:"
kubectl logs -f deployment/nexus-hub | grep ROTATED
```

---

## 7. Threat Model

Using **STRIDE** framework adapted for single-tenant personal use.

### 7.1 Spoofing

#### Threat 7.1.1: Rogue Spoke Registration

**Description:** Attacker registers a malicious spoke claiming to be Work-Laptop, gains access to hub.

**Likelihood:** Medium (if PSK is weak or compromised)
**Impact:** High (rogue spoke can receive job assignments, observe activity)

**Mitigations:**
- PSK generated as 256-bit random (high entropy, not guessable)
- PSK hashed with PBKDF2-10K before storage in hub database
- Optional mTLS for additional identity verification
- Audit log records all spoke registrations; operator reviews periodically

**Verification:**
```csharp
[Test]
public void WeakPsk_Should_FailAuthentication()
{
    var weakPsk = "password123";  // Weak
    var spokeRegRequest = new SpokeRegistrationPayload
    {
        SpokeId = Guid.NewGuid(),
        Signature = ComputeHmacSha256($"nonce{spokeId}", weakPsk)
    };

    // Should fail because hub has strong PSK stored
    var result = _hub.ValidateSignature(spokeRegRequest);
    Assert.False(result);
}
```

#### Threat 7.1.2: Rogue Hub (Impersonation)

**Description:** Attacker creates fake hub, tricks spoke into connecting and leaking data.

**Likelihood:** Low (operator controls hub URL; hardcoded or in config)
**Impact:** High (spoke could leak status, job plans, summaries)

**Mitigations:**
- Hub URL in spoke config is hardcoded or verified from config file
- TLS certificate validation: spoke validates hub cert (or pinned cert)
- Spoke logs warning if hub certificate changes unexpectedly
- Spoke requires explicit user approval to change hub URL

**Verification:**
```csharp
[Test]
public void SpokeRejectsUntrustedCertificate()
{
    var spokeConnector = new SpokeConnector
    {
        HubUrl = "https://malicious-hub.fake",
        ValidateCertificate = true,
        TrustedCertThumbprint = "expected_hub_cert_thumbprint"
    };

    // Should throw on cert mismatch
    Assert.ThrowsAsync<HttpRequestException>(
        async () => await spokeConnector.ConnectAsync());
}
```

### 7.2 Tampering

#### Threat 7.2.1: Man-in-the-Middle (MITM) Attack

**Description:** Attacker intercepts spoke↔hub traffic, modifies job assignments or output.

**Likelihood:** Very Low (TLS 1.3 in transit, private network via Tailscale)
**Impact:** High (attacker could inject malicious jobs, modify output)

**Mitigations:**
- All traffic over TLS 1.3 (authenticated encryption)
- Certificate pinning (optional, for high-security deployments)
- Spoke verifies TLS cert; hub verifies spoke PSK signature
- Messages have correlation IDs; hub detects out-of-order or replay

**Verification:**
```csharp
[Test]
public void JobAssignmentRequiresValidSignature()
{
    var jobAssignment = new JobAssignmentPayload
    {
        JobId = Guid.NewGuid(),
        SpokeId = Guid.NewGuid()
    };

    // Attacker modifies payload in transit
    jobAssignment.JobId = Guid.NewGuid();  // Changed

    // Spoke verifies HMAC over entire message; fails
    var isValid = VerifyHmacSignature(jobAssignment, expectedMac);
    Assert.False(isValid);
}
```

#### Threat 7.2.2: Job Output Tampering

**Description:** Attacker modifies job output while in transit (e.g., insert malicious code into summary).

**Likelihood:** Very Low (same as 7.2.1)
**Impact:** Medium (user could act on false information)

**Mitigations:**
- TLS 1.3 encryption (provides integrity)
- Timestamp on output chunks; detect out-of-order delivery
- Job output stored immutably in database; audit trail shows original submission
- User reviews output before acting on it (especially code diffs)

### 7.3 Repudiation

#### Threat 7.3.1: User Denies Approving Job

**Description:** User claims they didn't approve job; no record of approval.

**Likelihood:** Low (accidental); Very Low (intentional — user controls hub)
**Impact:** Low (personal tool, no third parties)

**Mitigations:**
- Every job approval logged in audit table with timestamp, user ID, approval details
- Immutable audit log (append-only, no deletion)
- User can review audit trail: `./nexus audit-log --job-id <id>`

**Verification:**
```csharp
[Test]
public async Task ApprovalRecordedInAuditLog()
{
    var job = new Job { Id = Guid.NewGuid(), Status = JobStatus.AwaitingApproval };
    var userId = "user@example.com";

    await _jobService.ApproveJobAsync(job.Id, userId);

    var auditEntry = await _auditService.GetAsync(new AuditQuery
    {
        Action = "JOB_APPROVED",
        JobId = job.Id
    });

    Assert.NotNull(auditEntry);
    Assert.Equal(userId, auditEntry.UserId);
    Assert.NotNull(auditEntry.Timestamp);
}
```

### 7.4 Information Disclosure

#### Threat 7.4.1: Source Code Leakage

**Description:** Worker output containing source code is transmitted to hub; exposes proprietary code.

**Likelihood:** Very Low (architectural boundary, not data on hub)
**Impact:** Medium (if exposed, code visibility on hub; actual source stays on spoke)

**Mitigations:**
- **Architectural:** Hub never receives full source code or diffs. Workers write output to spoke only.
- Spoke extracts summaries/plans and sends only safe content to hub
- Worker containers have read-only repo mounts; cannot output source to hub
- Full source code available on spoke machine only (where it is already stored)
- Hub UI shows conversation and status, not code diffs or raw output

**Rationale:**
Since worker output never leaves the spoke machine, and hub only receives summaries, the risk of code leakage is eliminated. The boundary is enforced by architecture, not by heuristics or filtering.
```

#### Threat 7.4.2: Credential Leakage (Hub Compromise)

**Description:** Hub database is compromised; attacker extracts hub credentials or spoke PSKs.

**Likelihood:** Low (private network, auth-gated)
**Impact:** High (credentials can be used to impersonate hub or spokes)

**Mitigations:**
- PSK stored hashed (PBKDF2), not plaintext; cannot be reversed
- OAuth client secret never stored (obtained from Google, ephemeral JWT issued)
- JWT signing key stored in secrets vault (k8s secret or .env)
- Database encrypted at rest (LUKS + OS-level)
- Regular backups of hub encrypted
- Audit log shows all database access; monitor for unusual queries

#### Threat 7.4.3: Spoke Workspace Compromise

**Description:** Attacker gains access to spoke machine, reads local credentials or source code.

**Likelihood:** Medium (local machine attack, not hub-specific)
**Impact:** High (attacker has all local credentials and access)

**Mitigations:**
- Local workspace encrypted via OS-level encryption (FileVault, BitLocker, LUKS)
- PSK and external credentials stored in OS keychain (not readable from disk)
- Git/Jira credentials managed by OS credential manager (not plaintext in files)
- File permissions: `chmod 700` on secrets directories
- Regular OS security updates

### 7.5 Denial of Service (DoS)

#### Threat 7.5.1: Spoke Flooding Hub

**Description:** Rogue spoke sends excessive job.output events, overwhelming hub's event processing.

**Likelihood:** Low (operator controls spoke creation)
**Impact:** Medium (hub becomes unresponsive)

**Mitigations:**
- Rate limiting on output submission: max 100 output chunks per job per second
- Backpressure: if output queue exceeds threshold, speak SlowDown message
- Output chunk size limit: max 64KB per chunk
- Job resource limits: container memory/CPU limits prevent runaway resource usage

**Implementation:**
```csharp
public class RateLimitMiddleware
{
    private readonly Dictionary<Guid, RateLimiter> _jobLimiters = new();
    private readonly IOptions<RateLimitOptions> _options;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/jobs/") &&
            context.Request.Path.EndsWithSegments("/output"))
        {
            var jobId = Guid.Parse(context.Request.RouteValues["jobId"].ToString());

            if (!_jobLimiters.TryGetValue(jobId, out var limiter))
            {
                limiter = new RateLimiter(_options.Value.OutputChunksPerSecond);
                _jobLimiters[jobId] = limiter;
            }

            if (!limiter.AllowRequest())
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded" });
                return;
            }
        }

        await _next(context);
    }
}
```

#### Threat 7.5.2: Job Approval Bottleneck

**Description:** Hub requires approval for every job; user approval queue grows unbounded, blocking work.

**Likelihood:** Low (user controls approval gates)
**Impact:** Low (degraded UX, not security)

**Mitigations:**
- Approval gates configurable per spoke/project; user can auto-approve low-risk jobs
- Batch approval: approve multiple jobs at once
- Approval timeout: jobs not approved within 24h are auto-cancelled
- Hub UI shows pending approvals with count and age

### 7.6 Elevation of Privilege

#### Threat 7.6.1: Worker Container Escape

**Description:** Attacker escapes worker container, gains access to spoke machine.

**Likelihood:** Very Low (Docker security, container isolation; container has no network)
**Impact:** High (attacker on spoke = access to all local credentials and source)

**Mitigations:**
- Container runs as unprivileged user (`USER 1000:1000`)
- Drop all capabilities: `CapDrop: [ALL]`
- Read-only root filesystem: `ReadonlyRootfs: true` (only `/tmp` writable)
- No network access: `NetworkMode: none`
- Resource limits: prevent forkbomb (CpuQuota, Memory limits)
- Regular Docker engine updates (security patches)

**Verification:**
```csharp
[Test]
public async Task WorkerContainerIsUnprivileged()
{
    var param = new CreateContainerParameters { ... };

    Assert.Equal("1000:1000", param.User);
    Assert.Contains("ALL", param.HostConfig.CapDrop);
    Assert.True(param.HostConfig.ReadonlyRootfs);
    Assert.Equal("none", param.HostConfig.NetworkMode);
    Assert.NotNull(param.HostConfig.Memory);
    Assert.NotNull(param.HostConfig.CpuQuota);
}
```

#### Threat 7.6.2: Spoke ↔ Hub Auth Bypass

**Description:** Attacker bypasses PSK verification, sends commands to hub as trusted spoke.

**Likelihood:** Very Low (PSK verified on every message, HMAC signed)
**Impact:** High (attacker can assign jobs, observe activity)

**Mitigations:**
- Every SignalR method in NexusHub requires PSK validation (no exception routes)
- Signature verified before method body executes (not delegated to service)
- Failed authentication immediately aborts connection (Context.Abort())
- Audit log every failed auth attempt with IP, timestamp, PSK

**Code Example:**
```csharp
public class NexusHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var spokePsk = Context.Items["SpokePsk"] as string;
        if (string.IsNullOrEmpty(spokePsk))
        {
            Context.Abort();  // Fail fast
            _logger.LogWarning($"Unauthorized connection attempt from {Context.ConnectionId}");
            return;
        }

        await base.OnConnectedAsync();
    }

    public async Task SpokeRegisterAsync(SpokeRegistrationPayload payload)
    {
        // ALWAYS validate signature first
        var spokePsk = Context.Items["SpokePsk"] as string;
        var expectedSignature = HMAC(payload.Nonce, spokePsk);

        if (payload.Signature != expectedSignature)
        {
            Context.Abort();
            _logger.LogWarning($"Signature mismatch for {payload.SpokeId}");
            return;
        }

        // Safe to proceed
        // ... rest of method
    }
}
```

---

## 8. Audit & Logging

### 8.1 Hub Audit Log

**All API calls and SignalR events logged to PostgreSQL:**

```sql
CREATE TABLE "AuditLogs" (
    "Id" UUID PRIMARY KEY NOT NULL,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UserId" VARCHAR(255) NOT NULL,  -- Google account ID
    "Action" VARCHAR(100) NOT NULL,  -- AUTH_LOGIN, JOB_CREATED, JOB_APPROVED, etc.
    "ResourceType" VARCHAR(50),      -- Spoke, Job, Project, Message
    "ResourceId" VARCHAR(255),       -- spoke-id, job-id, project-id
    "Details" JSONB,                 -- Additional context (JSON)
    "IpAddress" VARCHAR(45),         -- IPv4 or IPv6
    "StatusCode" INTEGER,            -- HTTP or SignalR status
    "Duration" BIGINT                -- Request duration in ms
);
CREATE INDEX idx_audit_timestamp ON "AuditLogs"("Timestamp" DESC);
CREATE INDEX idx_audit_action ON "AuditLogs"("Action");
Create INDEX idx_audit_resource ON "AuditLogs"("ResourceType", "ResourceId");
```

**Audit Middleware (C#):**

```csharp
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        // Capture response
        var originalBodyStream = context.Response.Body;
        using (var memoryStream = new MemoryStream())
        {
            context.Response.Body = memoryStream;

            await _next(context);

            stopwatch.Stop();

            // Log to audit table
            var action = ExtractAction(context.Request.Path);
            await _auditService.LogAsync(new AuditEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                Action = action,
                ResourceType = ExtractResourceType(context.Request.Path),
                ResourceId = ExtractResourceId(context.Request.Path, context.RouteValues),
                IpAddress = ipAddress,
                StatusCode = context.Response.StatusCode,
                Duration = stopwatch.ElapsedMilliseconds,
                Details = new
                {
                    Method = context.Request.Method,
                    Path = context.Request.Path,
                    Query = context.Request.QueryString.ToString()
                }
            });

            // Copy response back
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }
    }

    private string ExtractAction(PathString path)
    {
        return path.Value switch
        {
            "/api/auth/google" => "AUTH_OAUTH",
            "/api/spokes/register" => "SPOKE_REGISTERED",
            "/api/jobs" => "JOB_CREATED",
            "/api/jobs/{id}/approve" => "JOB_APPROVED",
            "/api/projects" => "PROJECT_CREATED",
            _ => "API_CALL"
        };
    }
}
```

**Spoke-Side Logging:**

```csharp
// Spoke logs all hub events to local file
public class SpokeEventLogger
{
    private readonly string _logFile = Path.Expand("~/.nexus/logs/events.log");

    public void LogEvent(SpokeEvent e)
    {
        var entry = new
        {
            Timestamp = DateTime.UtcNow,
            EventType = e.Type,
            JobId = e.JobId,
            Status = e.Status,
            Details = e.Details
        };

        var json = JsonSerializer.Serialize(entry);
        File.AppendAllText(_logFile, json + Environment.NewLine);
    }
}

// Rotate logs daily
[Timer("0 0 * * *")]  // Midnight daily
public void RotateLogsAsync()
{
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var archivePath = Path.Expand($"~/.nexus/logs/events-{today}.log");

    if (File.Exists(_logFile))
    {
        File.Move(_logFile, archivePath, overwrite: true);
    }
}
```

### 8.2 Log Retention Policy

- **Hub audit log:** Retained for 1 year (configurable)
- **Spoke event log:** Retained for 90 days locally (older entries archived)
- **Job output:** Retained for 1 year (immutable, for compliance/review)
- **Compression:** Logs older than 30 days gzipped; stored in cold storage if available

**Cleanup Job (C# Quartz):**

```csharp
public class AuditLogCleanupJob : IJob
{
    private readonly IAuditRepository _repository;
    private readonly ILogger<AuditLogCleanupJob> _logger;

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoffDate = DateTime.UtcNow.AddYears(-1);
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

        _logger.LogInformation($"Deleted {deletedCount} audit logs older than {cutoffDate}");
    }
}

// Register in Quartz
services.AddQuartz(q =>
{
    var jobKey = new JobKey("AuditLogCleanup");
    q.AddJob<AuditLogCleanupJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("AuditLogCleanup-trigger")
        .WithCronSchedule("0 2 1 * *"));  // 2 AM on 1st of month
});
```

### 8.3 Log Query & Review

**CLI tools for operator:**

```bash
# View recent audit logs
nexus audit-log --limit 100

# Filter by action
nexus audit-log --action JOB_APPROVED --limit 50

# Filter by resource
nexus audit-log --resource-type Job --resource-id <job-id>

# Filter by user and date range
nexus audit-log --user user@example.com --since 2026-03-01 --until 2026-04-01

# Export for compliance
nexus audit-log --export json --output audit-report.json

# Search logs for suspicious activity
nexus audit-log --action FAILED_AUTH | grep -c "Signature mismatch"
```

---

## 9. Incident Response

### 9.1 Spoke PSK Compromise

**Scenario:** Spoke PSK leaked or suspected compromised (e.g., developer left on USB drive).

**Immediate Actions:**
1. Identify compromised spoke: `nexus list-spokes`
2. Revoke current PSK: `nexus spoke revoke-psk --spoke-id <id>`
3. Deregister spoke temporarily: `nexus spoke deregister --spoke-id <id>` (hub stops accepting connections)
4. Review audit log for unauthorized activity: `nexus audit-log --spoke-id <id> --action SPOKE_REGISTERED`
5. Generate new PSK: `nexus spoke generate-psk --spoke-id <id>`
6. Distribute new PSK to operator (manually or via secure channel)
7. Restart spoke daemon with new PSK
8. Re-register spoke: `nexus spoke register --spoke-id <id> --psk <new-psk>`
9. Monitor for suspicious activity for 24 hours

**Prevention:**
- Rotate PSK quarterly (automatic reminder)
- Store PSK in OS keychain, not plaintext
- Document spoke PSK rotation in runbook

### 9.2 Hub Compromise (Database Breach)

**Scenario:** Hub database compromised; attacker reads entire PostgreSQL database.

**Immediate Actions:**
1. Isolate hub: disconnect from network or restrict access via Tailscale
2. Preserve evidence: snapshot VM/disk for forensics
3. Assess exposure: review audit log for what was accessed and when
4. Revoke all session cookies: invalidate all active JWT tokens (optional if short-lived)
5. Rotate all secrets:
   - Spoke PSKs (via incident recovery procedure)
   - JWT signing key
   - PostgreSQL password
   - Google OAuth secret (request new from Google Cloud Console)
6. Restore hub from clean backup (if available) or redeploy from code
7. Audit affected spokes for unauthorized job approvals

**Long-Term:**
- Enable encryption at rest (LUKS) on hub database volume
- Implement regular encrypted backups
- Set up log aggregation (syslog to external secure server)
- Review and tighten network access controls

### 9.3 Rogue Spoke Registration

**Scenario:** Attacker creates new spoke registration without authorization.

**Detection:**
- Audit log shows unexpected `SPOKE_REGISTERED` event
- Hub UI shows new spoke in list
- Operator's notification alert (if configured)

**Response:**
1. Deregister spoke immediately: `nexus spoke deregister --spoke-id <id> --force`
2. Review audit log: who approved this? (check approval workflow)
3. Revoke any jobs assigned to that spoke
4. Review job output stream for data exfiltration
5. Strengthen spoke registration controls (optional: add approval gate for new spokes)

### 9.4 Job Output Containing Sensitive Data

**Scenario:** Worker output included source code or credentials despite filtering.

**Response:**
1. Retrieve job record and output stream: `nexus job inspect <job-id>`
2. Review what was sent to hub: `nexus job output <job-id> | grep -i secret`
3. Assess exposure: was sensitive data visible to multiple spokes or hub admins?
4. Delete contaminated output from database (audit log preserves original submission)
5. Revoke any credentials that were exposed (regenerate API keys, tokens)
6. Review and improve data boundary filter (add pattern, strengthen heuristics)
7. Retrain operator on data classification

---

## 10. Security Checklist

### 10.1 Pre-Deployment Checklist (Hub)

- [ ] PostgreSQL database initialized with strong admin password (20+ chars, random)
- [ ] Database backup configured (daily encrypted snapshots)
- [ ] TLS certificates provisioned (self-signed OK for private infrastructure, CA-signed for production)
- [ ] Google OAuth app created; Client ID and Secret stored in secrets vault (not in code)
- [ ] JWT signing key generated (256-bit random) and stored in secrets vault
- [ ] Spoke whitelist configured (no spokes pre-registered; operator adds during setup)
- [ ] Audit logging enabled; test log entry visible in database
- [ ] Rate limiting configured on output submission (100 chunks/sec default)
- [ ] Tailscale (or VPN) configured; hub not exposed on public internet
- [ ] Hub reverse proxy configured with TLS 1.3 enforcement
- [ ] CORS configured to allow only local origins (localhost or Tailscale subnet)
- [ ] Health check endpoint tested: `curl https://nexus.home.local/health`
- [ ] Kubernetes secrets (if k3s): no secrets hardcoded in manifests
- [ ] Secrets vault (if external): integration tested (e.g., HashiCorp Vault)
- [ ] Log rotation configured; old logs not consuming disk space
- [ ] Monitoring/alerting setup: alert on `FAILED_AUTH`, `JOB_OUTPUT_FILTERED`, `POD_CRASH`
- [ ] Disaster recovery plan documented: restore from backup procedure tested
- [ ] Operator runbook created: incident response procedures documented

### 10.2 Per-Spoke Onboarding Checklist

- [ ] Spoke machine has OS-level encryption enabled (LUKS, FileVault, BitLocker)
- [ ] Spoke machine has local firewall enabled
- [ ] Spoke machine has antivirus/malware detection running
- [ ] PSK generated (256-bit random) and stored in OS keychain
- [ ] Hub URL configured in spoke config (hardcoded or from env var, not user input)
- [ ] Spoke workspace directory created and permissions set (`chmod 700`)
- [ ] Secrets directories created with restrictive permissions
- [ ] Git credentials configured (via credential helper, not plaintext)
- [ ] Jira API token stored in OS keychain or encrypted config
- [ ] Spoke daemon can connect to hub (test: `curl -v https://nexus.home.local/health`)
- [ ] Spoke can authenticate to hub (check audit log for `SPOKE_REGISTERED`)
- [ ] Sample job created and executed; output verified not to contain source code
- [ ] Operator reviewed filtered output (if filtering occurred)
- [ ] Approval gates configured for spoke (auto-approve vs. require approval)
- [ ] Backup schedule for spoke workspace configured (daily encrypted backup)
- [ ] Documentation updated: which credentials live on this spoke, retention policy
- [ ] Spoke machine added to disaster recovery plan (how to provision new spoke if machine fails)
- [ ] MCP config (`spoke-mcp-config.json`) generated and tested (Jira, Git connections working)
- [ ] Pending commands cleanup job scheduled (expires old commands after 24 hours)

### 10.3 Quarterly Security Review

- [ ] Review audit log for anomalies (unusual spokes, failed auth, unexpected approvals)
- [ ] Verify all PSKs still hashed correctly (sample check)
- [ ] Verify all encryption keys still in secrets vault (not leaked to logs)
- [ ] Rotate PSKs (all spokes)
- [ ] Rotate JWT signing key
- [ ] Rotate PostgreSQL password
- [ ] Rotate Google OAuth secret (if possible; request new from Google Cloud)
- [ ] Review pending commands: any stuck or expired? Cleanup working correctly?
- [ ] Review PR pending actions: any stuck approvals? User review queue healthy?
- [ ] Verify backups are restorable (test restore to a test environment)
- [ ] Review and test incident response procedures (runbook walkthrough)
- [ ] Update threat model if architecture has changed
- [ ] Check for security updates: .NET, PostgreSQL, Docker, Linux kernel
- [ ] Document any changes and commit to repo (code, not secrets)

---

## 11. Deployment Security Checklist (First-Time Setup)

### Step 1: Secure Hub Infrastructure

```bash
# On machine hosting hub
sudo apt-get update && sudo apt-get upgrade -y

# Enable LUKS encryption on database volume
sudo lvcreate -L 100G -n nexus_data vg0
sudo cryptsetup luksFormat /dev/vg0/nexus_data
sudo cryptsetup luksOpen /dev/vg0/nexus_data nexus_data_decrypted
sudo mkfs.ext4 /dev/mapper/nexus_data_decrypted
sudo mount /dev/mapper/nexus_data_decrypted /var/lib/nexus-db

# Configure PostgreSQL
sudo postgresql-setup initdb --pgdata=/var/lib/nexus-db
sudo systemctl start postgresql

# Test database connection
psql -h localhost -U postgres
```

### Step 2: Generate Secrets

```bash
# JWT signing key (256-bit = 32 bytes = 44 chars base64)
openssl rand -base64 32

# Save to secrets vault
kubectl create secret generic nexus-secrets \
  --from-literal=jwt-signing-key="$(openssl rand -base64 32)" \
  --from-literal=postgres-password="$(openssl rand -base64 32)" \
  --from-literal=google-oauth-secret="<from Google Cloud Console>"

# Verify secrets created
kubectl get secrets nexus-secrets -o yaml
```

### Step 3: Deploy Hub

```bash
# Clone repo (must be private; do not expose secrets)
git clone https://github.com/nexus-ai/nexus.git
cd nexus/hub

# Build Docker image
docker build -t nexus-hub:latest .

# Deploy via docker-compose (local) or Kubernetes (self-hosted)
docker-compose up -d

# Verify running
docker ps | grep nexus-hub
docker logs nexus-hub

# Test health endpoint
curl https://localhost:5001/health
# Output: { "status": "healthy" }
```

### Step 4: Configure Tailscale/VPN

```bash
# Install Tailscale on hub machine
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up --authkey $TS_AUTHKEY

# Get Tailscale IP
tailscale ip -4
# e.g., 100.123.45.67

# Configure hub to listen on Tailscale IP
# appsettings.json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://100.123.45.67:5001"
      }
    }
  }
}

# Restart hub
docker restart nexus-hub

# Test from another machine on Tailnet
curl -k https://100.123.45.67:5001/health
```

### Step 5: First Spoke Registration

```bash
# Generate spoke PSK
openssl rand -base64 32
# Copy to clipboard

# On spoke machine, create config
mkdir -p ~/.nexus/.nexus
cat > ~/.nexus/.nexus/config.yaml <<EOF
hub:
  url: https://100.123.45.67:5001
  spoke_id: <uuid>  # Generate with `uuidgen` or online

capabilities:
  - jira
  - git
  - docker
EOF

# Store PSK in keychain
pass insert nexus/hub-psk
# Paste: <psk from clipboard>

# Start spoke daemon
./nexus-spoke daemon start

# Check hub audit log
kubectl logs deployment/nexus-hub | grep SPOKE_REGISTERED

# Verify spoke connected
nexus spoke list
# Output: ✓ Work-Laptop (online)
```

---

## 12. Summary

**Nexus Security Model** for single-tenant personal AI orchestration:

| Component | Threat | Mitigation |
|---|---|---|
| **Hub UI** | Unauthorized access | Google OAuth + whitelist + JWT |
| **Spoke Auth** | Rogue spoke registration | PSK (256-bit) + PBKDF2-hashed + optional mTLS |
| **Network** | MITM attack | TLS 1.3 + Tailscale/Cloudflare Tunnel |
| **Data Boundary** | Source code leakage | Spoke-side filtering + pattern detection |
| **Credentials** | Plaintext storage | OS keychain + encrypted config |
| **Database** | Breach exposure | PBKDF2 hashes + optional TDE + encrypted backups |
| **Worker Isolation** | Container escape | Unprivileged user + dropped caps + read-only FS |
| **Auditability** | Repudiation | Immutable audit log (1 year retention) |
| **Incident Response** | Slow recovery | Documented runbooks + quarterly drills |

**Implementation Priority:**
1. Phase 1 (Foundation): OAuth, PSK auth, TLS, audit log
2. Phase 2 (Hardening): Data boundary filter, mTLS option, encryption at rest
3. Phase 3 (Operations): Log aggregation, monitoring, backup automation
4. Phase 4 (Maturity): Incident response automation, zero-trust network

---

**Document Version:** 1.0
**Next Review:** 2026-07-04 (Quarterly)
**Owner:** Project Maintainer (Architecture)

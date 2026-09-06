# Firebase Authentication emulator

Google's official Auth emulator (part of the Firebase Local Emulator Suite) wrapped in a container.
It speaks the same Identity Toolkit REST API and works with the same client and Admin SDKs as
production, but runs fully offline against the demo project `demo-asmpt`: no Firebase project, no
credentials, no calls to Google.

## Run

The emulator is a service of the repository's root `compose.yaml`, next to PostgreSQL:

```bash
docker compose up -d --build firebase-auth   # from the repository root; omit the service name to start everything
```

| Endpoint | URL |
|---|---|
| Auth emulator (SDKs and REST) | http://localhost:9099 |
| Emulator UI (browse, add and edit users) | http://localhost:4000/auth |
| REST API spec | http://localhost:9099/emulator/openapi.json |

`docker compose stop` / `down` exports all accounts to a named volume first, so users survive
restarts. `docker compose down -v` discards them; the next start imports `seed/` again.

## Seed account

`seed/` is a checked-in emulator export, imported whenever the volume is empty.

| Email | Password | Custom claims |
|---|---|---|
| dev@example.com | Password1! | `{"role":"admin"}` |

Emulator exports store passwords in clear text (`fakeHash:...:password=...`). That is fine for a
seed and one more reason never to point this at real user data. To change the seed: edit users in
the UI, run `docker compose stop firebase-auth` from the repository root, then
`docker cp asmpt-firebase-auth:/srv/firebase/data/export/. docker/firebase-auth-emulator/seed/`.

## Connecting from the apps

**Browser (Firebase JS SDK, e.g. via JS interop in Blazor):** call
`connectAuthEmulator(auth, "http://localhost:9099")` right after `getAuth()`. Any non-empty
`apiKey` is accepted; use `projectId: "demo-asmpt"` so the token audience matches.

**Server-side REST (e.g. from Blazor Server):** production URLs with the host swapped, for example
`http://localhost:9099/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=any`.

**Admin SDK (`FirebaseAdmin` NuGet):** set `FIREBASE_AUTH_EMULATOR_HOST=localhost:9099`. The SDK
then talks to the emulator and accepts its unsigned tokens in `VerifyIdTokenAsync`. Admin calls
without the SDK (custom claims, listing users) use `Authorization: Bearer owner`, e.g.
`POST /identitytoolkit.googleapis.com/v1/projects/demo-asmpt/accounts:update`.

**Validating ID tokens in Service.Api with JwtBearer:** emulator tokens are unsigned (`alg: none`),
so a Development-only relaxation is needed; the Service implements it in `Service/Service.Api/Auth/ConfigureJwtBearerOptions.cs`. Verified with Microsoft.IdentityModel.JsonWebTokens 8.22:

```csharp
// Production
options.Authority = "https://securetoken.google.com/<projectId>";
options.TokenValidationParameters = new()
{
    ValidIssuer = "https://securetoken.google.com/<projectId>",
    ValidAudience = "<projectId>",
};

// Development against the emulator: no Authority (there is no key set to fetch), and
options.TokenValidationParameters = new()
{
    ValidIssuer = "https://securetoken.google.com/demo-asmpt",
    ValidAudience = "demo-asmpt",
    RequireSignedTokens = false,       // NEVER in production
    ValidateIssuerSigningKey = false,
};
```

Issuer, audience and lifetime are still validated; only the signature check is skipped.

## What the emulator does not do

- Tokens are unsigned. Production validation must stay strict (see above).
- No emails or SMS are sent. Verification and reset links and SMS codes show up in the UI and at
  `/emulator/v1/projects/demo-asmpt/oobCodes` and `/emulator/v1/projects/demo-asmpt/verificationCodes`.
- OAuth providers (Google, Microsoft, ...) are simulated with a built-in fake account picker.
- No reCAPTCHA, App Check, rate limiting or abuse protection.

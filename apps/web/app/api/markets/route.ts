const backendURL = process.env.BACKEND_URL ?? "http://localhost:8080";

export const dynamic = "force-dynamic";

export async function GET() {
  try {
    const response = await fetch(`${backendURL}/api/v1/markets`, {
      cache: "no-store",
      signal: AbortSignal.timeout(3000),
    });

    const body = await response.text();

    return new Response(body, {
      status: response.status,
      headers: {
        "content-type":
          response.headers.get("content-type") ?? "application/json",
        "cache-control": "no-store",
      },
    });
  } catch {
    return Response.json(
      {
        error: {
          code: "backend_unavailable",
          message: "The market service could not be reached.",
        },
      },
      { status: 503 },
    );
  }
}

const backendURL = process.env.BACKEND_URL ?? "http://localhost:8080";
const defaultTimeoutMilliseconds = 10_000;
const simulationTimeoutMilliseconds = 900_000;

export const dynamic = "force-dynamic";

async function proxy(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  const { path } = await context.params;
  const backendPath = path.join("/");
  const url = new URL(`/api/v1/${path.join("/")}`, backendURL);
  url.search = new URL(request.url).search;

  const headers = new Headers();
  const authorization = request.headers.get("authorization");
  const contentType = request.headers.get("content-type");
  const requestId = request.headers.get("x-request-id");

  if (authorization) headers.set("authorization", authorization);
  if (contentType) headers.set("content-type", contentType);
  if (requestId) headers.set("x-request-id", requestId);

  const hasBody = !["GET", "HEAD"].includes(request.method);
  const timeoutMilliseconds = backendPath === "admin/simulations"
    ? simulationTimeoutMilliseconds
    : defaultTimeoutMilliseconds;

  try {
    const response = await fetch(url, {
      method: request.method,
      headers,
      body: hasBody ? await request.arrayBuffer() : undefined,
      cache: "no-store",
      signal: AbortSignal.timeout(timeoutMilliseconds),
    });

    return new Response(response.body, {
      status: response.status,
      headers: {
        "content-type":
          response.headers.get("content-type") ?? "application/json",
        "cache-control": "no-store",
      },
    });
  } catch (error) {
    const timedOut = error instanceof Error && error.name === "TimeoutError";
    return Response.json(
      {
        error: {
          code: timedOut ? "backend_timeout" : "backend_unavailable",
          message: timedOut
            ? `The backend operation exceeded ${timeoutMilliseconds / 1000} seconds.`
            : "The backend service could not be reached.",
        },
      },
      { status: timedOut ? 504 : 503 },
    );
  }
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const DELETE = proxy;
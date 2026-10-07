const backendURL = process.env.BACKEND_URL ?? "http://localhost:8080";

export const dynamic = "force-dynamic";

async function proxy(
  request: Request,
  context: { params: Promise<{ path: string[] }> },
) {
  const { path } = await context.params;
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

  try {
    const response = await fetch(url, {
      method: request.method,
      headers,
      body: hasBody ? await request.arrayBuffer() : undefined,
      cache: "no-store",
      signal: AbortSignal.timeout(10000),
    });

    return new Response(response.body, {
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
          message: "The backend service could not be reached.",
        },
      },
      { status: 503 },
    );
  }
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const DELETE = proxy;
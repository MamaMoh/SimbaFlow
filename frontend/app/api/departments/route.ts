import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";
import { getErrorStatus } from "@/lib/api/helpers";

export async function GET() {
  try {
    const data = await apiClient.get("/departments");
    return NextResponse.json(data);
  } catch (e: unknown) {
    const message = e instanceof Error ? e.message : "Failed to fetch departments";
    return NextResponse.json({ error: message }, { status: getErrorStatus(e) });
  }
}



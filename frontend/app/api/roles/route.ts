import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function GET() {
  try {
    const data = await apiClient.get("/roles");
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to fetch roles" },
      { status: e?.status || 500 }
    );
  }
}



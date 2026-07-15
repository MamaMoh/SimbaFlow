import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const data = await apiClient.post("/auth/forgot-password", body);
    return NextResponse.json(data ?? { success: true });
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to request password reset" },
      { status: e?.status || 500 }
    );
  }
}



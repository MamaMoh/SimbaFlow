import { NextRequest, NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const data = await apiClient.post("/auth/change-password", body);
    return NextResponse.json(data ?? { success: true });
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to change password" },
      { status: e?.status || 500 }
    );
  }
}



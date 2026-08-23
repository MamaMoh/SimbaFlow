import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const data = await apiClient.post("/auth/reset-password", body);
    return NextResponse.json(data ?? { isSuccess: true });
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Could not reset the password" },
      { status: e?.status || 500 }
    );
  }
}

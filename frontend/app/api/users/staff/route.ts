import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function GET() {
  try {
    const data = await apiClient.get("/users/staff");
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to fetch staff" },
      { status: e?.status || 500 }
    );
  }
}

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const data = await apiClient.post("/users/staff", body);
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to create staff" },
      { status: e?.status || 500 }
    );
  }
}



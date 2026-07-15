import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function GET() {
  try {
    const data = await apiClient.get("/users");
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to fetch users" },
      { status: e?.status || 500 }
    );
  }
}

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const data = await apiClient.post("/users", body);
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to create user" },
      { status: e?.status || 500 }
    );
  }
}



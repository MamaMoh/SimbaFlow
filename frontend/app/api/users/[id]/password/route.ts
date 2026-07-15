import { NextResponse } from "next/server";
import { apiClient } from "@/lib/api/client";

export async function POST(
  req: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params;
    const body = await req.json();
    const data = await apiClient.post(`/users/${id}/password`, body);
    return NextResponse.json(data);
  } catch (e: any) {
    return NextResponse.json(
      { error: e?.message || "Failed to change password" },
      { status: e?.status || 500 }
    );
  }
}


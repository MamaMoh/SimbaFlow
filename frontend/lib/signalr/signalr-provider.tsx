"use client";

import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useSession } from "next-auth/react";

type ConnectionStatus = "connecting" | "connected" | "disconnected" | "reconnecting";

interface SignalRContextValue {
  connection: HubConnection | null;
  status: ConnectionStatus;
  subscribe: (event: string, handler: (...args: unknown[]) => void) => void;
  unsubscribe: (event: string, handler: (...args: unknown[]) => void) => void;
}

const SignalRContext = createContext<SignalRContextValue>({
  connection: null,
  status: "disconnected",
  subscribe: () => {},
  unsubscribe: () => {},
});

export function SignalRProvider({ children }: { children: ReactNode }) {
  const { data: session } = useSession();
  const [status, setStatus] = useState<ConnectionStatus>("disconnected");
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const token = (session as any)?.accessToken;
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/hubs/simbaflow`, {
        accessTokenFactory: () => token as string,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.onreconnecting(() => setStatus("reconnecting"));
    connection.onreconnected(() => setStatus("connected"));
    connection.onclose(() => setStatus("disconnected"));

    connectionRef.current = connection;
    setStatus("connecting");

    connection
      .start()
      .then(() => setStatus("connected"))
      .catch((err) => {
        console.error("SignalR connection failed:", err);
        setStatus("disconnected");
      });

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [(session as any)?.accessToken]);

  const subscribe = (event: string, handler: (...args: unknown[]) => void) => {
    connectionRef.current?.on(event, handler);
  };

  const unsubscribe = (event: string, handler: (...args: unknown[]) => void) => {
    connectionRef.current?.off(event, handler);
  };

  return (
    <SignalRContext.Provider
      value={{ connection: connectionRef.current, status, subscribe, unsubscribe }}
    >
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalR() {
  return useContext(SignalRContext);
}

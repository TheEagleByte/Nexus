"use client";

import {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import type { SpokeResponse } from "@/types/api";

type ConnectionState = "Disconnected" | "Connecting" | "Connected" | "Reconnecting";

interface SignalRContextValue {
  connectionState: ConnectionState;
  spokes: Map<string, SpokeResponse>;
}

const SignalRContext = createContext<SignalRContextValue>({
  connectionState: "Disconnected",
  spokes: new Map(),
});

export function useSignalR() {
  return useContext(SignalRContext);
}

function mapHubState(state: HubConnectionState): ConnectionState {
  switch (state) {
    case HubConnectionState.Connected:
      return "Connected";
    case HubConnectionState.Connecting:
      return "Connecting";
    case HubConnectionState.Reconnecting:
      return "Reconnecting";
    default:
      return "Disconnected";
  }
}

interface SignalRProviderProps {
  hubUrl: string;
  children: ReactNode;
}

export function SignalRProvider({ hubUrl, children }: SignalRProviderProps) {
  const [connectionState, setConnectionState] =
    useState<ConnectionState>("Disconnected");
  const [spokes, setSpokes] = useState<Map<string, SpokeResponse>>(new Map());
  const connectionRef = useRef<HubConnection | null>(null);

  const updateSpoke = useCallback(
    (id: string, updates: Partial<SpokeResponse>) => {
      setSpokes((prev) => {
        const next = new Map(prev);
        const existing = next.get(id);
        if (existing) {
          next.set(id, { ...existing, ...updates });
        } else {
          next.set(id, updates as SpokeResponse);
        }
        return next;
      });
    },
    []
  );

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Track connection state
    connection.onreconnecting(() => setConnectionState("Reconnecting"));
    connection.onreconnected(() => setConnectionState("Connected"));
    connection.onclose(() => setConnectionState("Disconnected"));

    // Subscribe to spoke events
    connection.on("SpokeRegistered", (data: { spoke: SpokeResponse }) => {
      updateSpoke(data.spoke.id, data.spoke);
    });

    connection.on(
      "HeartbeatAcknowledged",
      (data: { spokeId: string; timestamp: string }) => {
        updateSpoke(data.spokeId, {
          status: "online",
          lastSeen: data.timestamp,
        });
      }
    );

    connection.on(
      "JobStatusChanged",
      (data: { spokeId: string; jobId: string; status: string }) => {
        // Update spoke's active job count when jobs complete/fail
        if (
          data.status === "completed" ||
          data.status === "failed" ||
          data.status === "cancelled"
        ) {
          setSpokes((prev) => {
            const next = new Map(prev);
            const spoke = next.get(data.spokeId);
            if (spoke && spoke.activeJobCount > 0) {
              next.set(data.spokeId, {
                ...spoke,
                activeJobCount: spoke.activeJobCount - 1,
              });
            }
            return next;
          });
        }
      }
    );

    // Start connection
    setConnectionState("Connecting");
    connection
      .start()
      .then(() => setConnectionState("Connected"))
      .catch(() => setConnectionState("Disconnected"));

    return () => {
      connection.stop();
    };
  }, [hubUrl, updateSpoke]);

  return (
    <SignalRContext.Provider value={{ connectionState, spokes }}>
      {children}
    </SignalRContext.Provider>
  );
}

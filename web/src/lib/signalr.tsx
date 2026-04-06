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
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";
import type {
  SpokeResponse,
  JobStatusChangedEvent,
  JobOutputReceivedEvent,
  ProjectUpdatedEvent,
} from "@/types/api";

type ConnectionState = "Disconnected" | "Connecting" | "Connected" | "Reconnecting";

type JobOutputCallback = (chunk: JobOutputReceivedEvent) => void;

interface SignalRContextValue {
  connectionState: ConnectionState;
  spokes: Map<string, SpokeResponse>;
  jobUpdates: Map<string, JobStatusChangedEvent>;
  projectUpdates: Map<string, ProjectUpdatedEvent>;
  subscribeJobOutput: (jobId: string, cb: JobOutputCallback) => void;
  unsubscribeJobOutput: (jobId: string, cb: JobOutputCallback) => void;
}

const SignalRContext = createContext<SignalRContextValue>({
  connectionState: "Disconnected",
  spokes: new Map(),
  jobUpdates: new Map(),
  projectUpdates: new Map(),
  subscribeJobOutput: () => {},
  unsubscribeJobOutput: () => {},
});

export function useSignalR() {
  return useContext(SignalRContext);
}


interface SignalRProviderProps {
  hubUrl: string;
  children: ReactNode;
}

export function SignalRProvider({ hubUrl, children }: SignalRProviderProps) {
  const [connectionState, setConnectionState] =
    useState<ConnectionState>("Connecting");
  const [spokes, setSpokes] = useState<Map<string, SpokeResponse>>(new Map());
  const [jobUpdates, setJobUpdates] = useState<Map<string, JobStatusChangedEvent>>(new Map());
  const [projectUpdates, setProjectUpdates] = useState<Map<string, ProjectUpdatedEvent>>(new Map());
  const connectionRef = useRef<HubConnection | null>(null);
  const outputListenersRef = useRef<Map<string, Set<JobOutputCallback>>>(new Map());

  const updateSpoke = useCallback(
    (id: string, updates: Partial<SpokeResponse>) => {
      setSpokes((prev) => {
        const next = new Map(prev);
        const existing = next.get(id);
        if (existing) {
          next.set(id, { ...existing, ...updates });
        } else if ("name" in updates && "capabilities" in updates) {
          next.set(id, updates as SpokeResponse);
        }
        return next;
      });
    },
    []
  );

  const subscribeJobOutput = useCallback((jobId: string, cb: JobOutputCallback) => {
    const listeners = outputListenersRef.current;
    if (!listeners.has(jobId)) {
      listeners.set(jobId, new Set());
    }
    listeners.get(jobId)!.add(cb);
  }, []);

  const unsubscribeJobOutput = useCallback((jobId: string, cb: JobOutputCallback) => {
    const listeners = outputListenersRef.current;
    const set = listeners.get(jobId);
    if (set) {
      set.delete(cb);
      if (set.size === 0) listeners.delete(jobId);
    }
  }, []);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.onreconnecting(() => setConnectionState("Reconnecting"));
    connection.onreconnected(() => setConnectionState("Connected"));
    connection.onclose(() => setConnectionState("Disconnected"));

    // Spoke events
    connection.on("SpokeRegistered", (data: { spoke: SpokeResponse }) => {
      updateSpoke(data.spoke.id, data.spoke);
    });

    connection.on(
      "HeartbeatAcknowledged",
      (spokeId: string, timestamp: string) => {
        updateSpoke(spokeId, {
          status: "online",
          lastSeen: timestamp,
        });
      }
    );

    // Job status changes
    connection.on("JobStatusChanged", (data: JobStatusChangedEvent) => {
      // Update job tracking
      setJobUpdates((prev) => {
        const next = new Map(prev);
        next.set(data.jobId, data);
        return next;
      });

      // Update spoke's active job count when jobs complete/fail
      if (
        data.newStatus === "completed" ||
        data.newStatus === "failed" ||
        data.newStatus === "cancelled"
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
    });

    // Job output streaming — dispatch to targeted listeners
    connection.on("JobOutputReceived", (data: JobOutputReceivedEvent) => {
      const listeners = outputListenersRef.current.get(data.jobId);
      if (listeners) {
        for (const cb of listeners) {
          cb(data);
        }
      }
    });

    // Project status updates
    connection.on("ProjectUpdated", (data: ProjectUpdatedEvent) => {
      setProjectUpdates((prev) => {
        const next = new Map(prev);
        next.set(data.projectId, data);
        return next;
      });
    });

    // Start connection
    connection
      .start()
      .then(() => setConnectionState("Connected"))
      .catch(() => setConnectionState("Disconnected"));

    return () => {
      connection.stop();
    };
  }, [hubUrl, updateSpoke]);

  return (
    <SignalRContext.Provider
      value={{
        connectionState,
        spokes,
        jobUpdates,
        projectUpdates,
        subscribeJobOutput,
        unsubscribeJobOutput,
      }}
    >
      {children}
    </SignalRContext.Provider>
  );
}

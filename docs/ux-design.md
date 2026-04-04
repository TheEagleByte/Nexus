# Nexus UX Design Document

> A dark terminal/hacker-inspired design system for the Nexus Hub UI. Implementation-ready specifications for React + Next.js with shadcn/ui and Tailwind CSS.

---

## 1. Design Philosophy

Nexus is a **command center for an engineer**, not a corporate dashboard. The UX design reflects this:

- **Information density without clutter** — Every pixel serves a purpose. Data is layered, accessible, but never overwhelming.
- **Terminal-inspired, not retro** — Cyan accents, monospace type for data, dark backgrounds. Modern tools (Vercel, Linear) use this language too. No CRT effects, no pastiche.
- **Mobile-first, but desktop-powerful** — Checking status from your phone is a primary use case. Desktop enables deep work and real-time monitoring.
- **Real-time feels alive but calm** — Live updates, smooth state changes, responsive feedback. No jarring reloads or excessive animations.
- **Accessibility first, aesthetics second** — High contrast, clear focus states, keyboard navigable. Dark mode + light text by default meets WCAG AA.
- **Predictable interactions** — Users (senior engineers) understand terminals, CLIs, and Unix-like interfaces. Leverage those mental models.

---

## 2. Visual Foundation

### 2.1 Color System

The palette defines a dark, high-contrast theme optimized for 8-12 hour work sessions.

#### Base Layers (Backgrounds)

| Name | Hex | RGB | Use |
|------|-----|-----|-----|
| **Background (Base)** | `#0a0a0a` | 10, 10, 10 | Page background, full viewport |
| **Background (Surface)** | `#141414` | 20, 20, 20 | Card backgrounds, panels |
| **Background (Elevated)** | `#1a1a1a` | 26, 26, 26 | Modals, popovers, nested surfaces |
| **Background (Accent)** | `#242424` | 36, 36, 36 | Hover states on surfaces, subtle emphasis |

**Rationale:** 10-point gradation between layers. Close enough to feel unified, distinct enough for visual hierarchy. Avoids pure black (#000000) to reduce contrast fatigue; avoids gray to stay in the "dark" territory.

#### Text / Foreground

| Name | Hex | RGB | Use | Contrast* |
|------|-----|-----|-----|-----------|
| **Foreground (Primary)** | `#f5f5f5` | 245, 245, 245 | Body text, primary content | 18:1 vs base |
| **Foreground (Secondary)** | `#a0a0a0` | 160, 160, 160 | Labels, helper text, metadata | 8:1 vs base |
| **Foreground (Muted)** | `#6b6b6b` | 107, 107, 107 | Disabled, very subtle text | 5:1 vs base |
| **Foreground (Inverse)** | `#0a0a0a` | 10, 10, 10 | Text on light/accent backgrounds | Used on accents only |

**Contrast ratios measured on #0a0a0a background. All meet WCAG AA or better.**

#### Accent & Interactive

| Name | Hex | RGB | Use |
|------|-----|-----|-----|
| **Accent (Primary)** | `#00d9ff` | 0, 217, 255 | Buttons, links, active states, focus rings. Terminal cyan vibes. |
| **Accent (Secondary)** | `#0099cc` | 0, 153, 204 | Hover state, pressed state (darker than primary). |
| **Accent (Muted)** | `#004466` | 0, 68, 102 | Disabled accent, background for accent surfaces. |

**Rationale:** Cyan is iconic in terminal/hacker culture. Bright enough to pop on dark backgrounds. Secondary shade provides depth for interaction states. All three maintain good contrast.

#### Status Colors

| Name | Hex | Use | Contrast |
|------|-----|-----|----------|
| **Success** | `#10b981` | Online, completed, approved | 8:1 vs base |
| **Warning** | `#f59e0b` | Pending, attention needed | 5.5:1 vs base |
| **Error** | `#ef4444` | Offline, failed, critical | 6:1 vs base |
| **Info** | `#3b82f6` | Queued, informational | 4.5:1 vs base |

**Usage:** Apply as badge background, icon color, border accent, or text when paired with darker background.

#### Borders & Dividers

| Name | Hex | Use |
|------|-----|-----|
| **Border (Default)** | `#333333` | Card borders, input outlines, table dividers |
| **Border (Subtle)** | `#252525` | Very subtle separation, hover states |
| **Border (Strong)** | `#404040` | Emphasize structure, sections |

**Rationale:** Subtle but readable. Cards and surfaces feel connected without being mushy.

#### Tailwind CSS Configuration

Add this to `tailwind.config.ts`:

```typescript
export default {
  theme: {
    extend: {
      colors: {
        background: {
          DEFAULT: '#0a0a0a',
          surface: '#141414',
          elevated: '#1a1a1a',
          accent: '#242424',
        },
        foreground: {
          DEFAULT: '#f5f5f5',
          secondary: '#a0a0a0',
          muted: '#6b6b6b',
          inverse: '#0a0a0a',
        },
        accent: {
          DEFAULT: '#00d9ff',
          secondary: '#0099cc',
          muted: '#004466',
        },
        status: {
          success: '#10b981',
          warning: '#f59e0b',
          error: '#ef4444',
          info: '#3b82f6',
        },
        border: {
          DEFAULT: '#333333',
          subtle: '#252525',
          strong: '#404040',
        },
      },
    },
  },
};
```

#### shadcn/ui CSS Variables

Add to your `globals.css`:

```css
@layer base {
  :root {
    --background: 0 0% 4%;         /* #0a0a0a */
    --foreground: 0 0% 96%;        /* #f5f5f5 */
    --card: 0 0% 8%;               /* #141414 */
    --card-foreground: 0 0% 96%;   /* #f5f5f5 */
    --popover: 0 0% 10%;           /* #1a1a1a */
    --popover-foreground: 0 0% 96%;
    --muted: 0 0% 42%;             /* #a0a0a0 */
    --muted-foreground: 0 0% 42%;
    --accent: 187 100% 50%;        /* #00d9ff */
    --accent-foreground: 0 0% 4%;  /* #0a0a0a */
    --destructive: 0 84% 60%;      /* #ef4444 */
    --destructive-foreground: 0 0% 96%;
    --border: 0 0% 20%;            /* #333333 */
    --input: 0 0% 20%;
    --ring: 187 100% 50%;          /* Accent cyan for focus rings */
    --radius: 0.375rem;            /* 6px default border radius */
  }
}
```

---

### 2.2 Typography

#### Typeface Selection

- **Display / Headings:** `Geist Sans` or `Inter` (variable weight, clean, modern)
- **Body / UI:** Same sans-serif family (consistency, system fonts acceptable)
- **Monospace / Data:** `Geist Mono`, `JetBrains Mono`, or fallback to `ui-monospace` (terminal aesthetic)

**Rule:** Any data that represents status, timestamps, IDs, code, or system state should use monospace. Natural language and UI labels use sans-serif.

#### Scale & Hierarchy

All sizes use a 4px base unit (Tailwind default). Line heights set at 1.5 (readable, not tight).

| Level | Size | Weight | Line Height | Use |
|-------|------|--------|-------------|-----|
| **H1** | 32px (2rem) | 600 | 1.25 | Page title, modal title |
| **H2** | 24px (1.5rem) | 600 | 1.33 | Section heading, card title |
| **H3** | 18px (1.125rem) | 600 | 1.4 | Subsection, spoke name in sidebar |
| **H4** | 16px (1rem) | 500 | 1.5 | Small heading, form label |
| **Body (LG)** | 16px (1rem) | 400 | 1.5 | Primary body text, list items |
| **Body (Base)** | 14px (0.875rem) | 400 | 1.5 | Secondary text, descriptions |
| **Body (SM)** | 12px (0.75rem) | 400 | 1.5 | Captions, helper text, metadata |
| **Code** | 12px (0.75rem) | 400 | 1.6 | Inline code, terminal output snippets |

#### Examples

```css
/* H1 */
.h1 { @apply text-3xl font-semibold leading-tight text-foreground; }

/* Body */
.body { @apply text-base font-normal leading-relaxed text-foreground; }

/* Caption / Muted */
.caption { @apply text-xs font-normal text-foreground-secondary; }

/* Code / Monospace */
.code { @apply font-mono text-sm text-foreground; }
```

---

### 2.3 Spacing & Layout

#### Base Unit: 4px Grid

All spacing, sizing, and positioning uses multiples of 4px. Reduces decision paralysis, ensures alignment.

| Multiplier | Pixels | CSS Class | Use |
|-----------|--------|-----------|-----|
| 0.5 | 2px | (none) | Borders, hairlines only |
| 1 | 4px | `p-1` | Minimal padding inside tight components |
| 2 | 8px | `p-2` | Standard padding, small margins |
| 3 | 12px | `p-3` | Comfortable padding, section separators |
| 4 | 16px | `p-4` | Default card padding, standard gaps |
| 6 | 24px | `p-6` | Large sections, modal padding |
| 8 | 32px | `p-8` | Page padding, major layout spacing |

#### Layout Structure

**Desktop (≥1024px)**
```
┌─────────────────────────────────────────┐
│         Header (Navbar)                 │
├──────────────┬──────────────────────────┤
│              │                          │
│  Sidebar     │    Main Content          │
│  (240px)     │    (fluid, max-1200px)   │
│              │                          │
│              │                          │
└──────────────┴──────────────────────────┘
```

- Sidebar: 240px fixed width, scrollable, dark background (surface)
- Main: Flex-grow, padded with p-6 or p-8
- Max-width on main content: 1200px (prevents overscan on ultra-wide monitors)

**Tablet (768px—1023px)**
```
┌─────────────────────────┐
│     Header              │
├─────────────────────────┤
│                         │
│  Collapsible Sidebar +  │
│  Main Content           │
│  (Overlay drawer on     │
│   small screens)        │
│                         │
└─────────────────────────┘
```

- Sidebar collapses to a hamburger menu icon
- Drawer slides in as overlay when opened
- Full width content below header

**Mobile (<768px)**
```
┌─────────────────────────┐
│  Header                 │
│  (Logo + Menu toggle)   │
├─────────────────────────┤
│                         │
│  Main Content           │
│  (Full width)           │
│                         │
├─────────────────────────┤
│  Bottom Tab Bar         │
│  (5 main nav items)     │
└─────────────────────────┘
```

- Header with logo + menu toggle
- Full-width main content
- Fixed bottom tab bar (sticky footer)
- Sidebar content moves to bottom drawer or modal

#### Card & Spacing Patterns

**Card Template (shadcn-compatible)**
```jsx
<div className="rounded-md border border-border bg-card p-4">
  <h3 className="text-foreground font-semibold">Title</h3>
  <p className="text-foreground-secondary text-sm mt-2">Content</p>
</div>
```

**Group / Section**
```jsx
<section className="space-y-4">
  <h2 className="text-xl font-semibold">Section</h2>
  <div className="space-y-2">
    {/* Items with space-y-2 between */}
  </div>
</section>
```

---

## 3. Component Specifications

### 3.1 Navigation

#### Sidebar (Desktop)

- Width: 240px
- Background: `bg-background-surface`
- Border-right: `1px border-border`
- Fixed or sticky (up to scrolling breakpoint)
- Scrollable content when tall

**Structure:**
```
┌──────────────────┐
│  Logo / Branding │  (48px height, p-4)
├──────────────────┤
│                  │
│  Spoke List      │  (Primary nav)
│  ├─ Spoke 1      │
│  ├─ Spoke 2      │  Each 44px, hover: bg-accent
│  └─ Spoke 3      │
│                  │
├──────────────────┤
│  Secondary Nav   │  (Settings, Help, etc.)
│  ├─ Settings     │
│  └─ Docs         │
│                  │
└──────────────────┘
```

**Spoke Item Styling:**
```jsx
<button
  className={`
    w-full text-left px-4 py-3 rounded-sm
    text-foreground text-sm font-medium
    transition-colors duration-150
    ${isActive
      ? 'bg-accent text-background-elevated'
      : 'hover:bg-background-accent'}
  `}
>
  <div className="flex items-center gap-2">
    <div className={`w-2 h-2 rounded-full ${statusColor}`} />
    <span className="truncate">{spokeName}</span>
  </div>
  <div className="text-xs text-foreground-secondary mt-1">
    {jobCount} job{jobCount !== 1 ? 's' : ''}
  </div>
</button>
```

#### Header / Navbar

- Height: 56px
- Background: `bg-background-surface`
- Border-bottom: `1px border-border`
- Sticky/fixed at top
- Content: Logo, search, user menu (if applicable)

```jsx
<header className="h-14 border-b border-border bg-background-surface sticky top-0 z-50">
  <div className="max-w-screen-2xl mx-auto h-full px-4 sm:px-6 flex items-center justify-between">
    <div className="flex items-center gap-3">
      <h1 className="text-lg font-bold text-accent">Nexus</h1>
      {/* Breadcrumbs or page title here */}
    </div>
    {/* Search, user menu, etc. */}
  </div>
</header>
```

#### Mobile Navigation

**Bottom Tab Bar** (mobile only, `<768px`)

- Height: 56px
- Background: `bg-background-surface`
- Border-top: `1px border-border`
- Fixed bottom, full width
- 4–5 main nav items (Dashboard, Spokes, Projects, Jobs, Settings)
- Each tab shows icon + label (if room), or icon + badge

```jsx
<nav className="fixed bottom-0 left-0 right-0 h-14 border-t border-border bg-background-surface md:hidden">
  <div className="flex h-full items-center justify-around">
    {navItems.map(item => (
      <button
        key={item.path}
        className={`flex flex-col items-center justify-center w-full h-full text-xs font-medium ${
          isActive ? 'text-accent' : 'text-foreground-secondary'
        }`}
      >
        <item.Icon className="w-5 h-5 mb-1" />
        <span className="truncate">{item.label}</span>
      </button>
    ))}
  </div>
</nav>
```

#### Breadcrumbs

Light navigation indicator in header/page title area.

```jsx
<nav className="text-xs font-mono text-foreground-secondary">
  <span>Dashboard</span>
  <span className="mx-1">/</span>
  <span className="text-foreground">Work Laptop</span>
  <span className="mx-1">/</span>
  <span>PROJ-4521</span>
</nav>
```

#### Conversation List (Sidebar — Desktop)

Below the spoke list, show recent conversations. Conversations are grouped by spoke, with hub-level conversations at the top.

**Structure:**
```
Spokes List
│
├─ Conversation Header + New Thread Button
├─ Recent Conversations (sorted by updatedAt desc)
│  ├─ [Hub] Cross-system query about status
│  ├─ [Work Laptop] Notification service (3 msgs)
│  ├─ [Personal Server] Database schema review (12 msgs)
│  └─ ...more (scrollable)
```

**Conversation Item Styling:**
```jsx
<div className="border-b border-border-subtle p-3 cursor-pointer hover:bg-background-accent transition-colors">
  <div className="flex items-start justify-between gap-2">
    <div className="flex-1 min-w-0">
      <div className="text-xs text-foreground-secondary font-mono">
        {spokeId ? `[${spokeName}]` : `[Hub]`}
      </div>
      <div className="text-sm font-medium text-foreground truncate">
        {title}
      </div>
    </div>
    <div className="text-xs text-foreground-muted flex-shrink-0">
      {messageCount}
    </div>
  </div>
</div>
```

**New Thread Button:**
```jsx
<button className="w-full px-4 py-2 bg-accent text-background-inverse rounded font-medium hover:bg-accent-secondary transition-colors mb-3">
  + New Thread
</button>
```

---

### 3.2 Dashboard Cards (Spoke Overview)

Compact cards showing spoke status, active jobs, and last activity.

#### Spoke Status Card

Size: 320px wide (fits 3 per row on desktop, 1 on mobile)

```jsx
<div className="w-full max-w-sm rounded-md border border-border bg-card p-4 hover:border-accent transition-colors">
  {/* Header: Name + Status Badge */}
  <div className="flex items-start justify-between mb-3">
    <h3 className="text-base font-semibold text-foreground">{spokeName}</h3>
    <div className={`inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-mono ${
      status === 'online'
        ? 'bg-status-success/20 text-status-success'
        : 'bg-status-error/20 text-status-error'
    }`}>
      <div className={`w-1.5 h-1.5 rounded-full ${status === 'online' ? 'bg-status-success' : 'bg-status-error'}`} />
      {status}
    </div>
  </div>

  {/* Stats Row */}
  <div className="grid grid-cols-2 gap-3 mb-3">
    <div className="bg-background-accent rounded px-2 py-1">
      <div className="text-xs text-foreground-secondary">Jobs</div>
      <div className="text-lg font-mono font-semibold text-foreground">{activeJobs}</div>
    </div>
    <div className="bg-background-accent rounded px-2 py-1">
      <div className="text-xs text-foreground-secondary">Projects</div>
      <div className="text-lg font-mono font-semibold text-foreground">{projectCount}</div>
    </div>
  </div>

  {/* Last Activity */}
  <div className="text-xs text-foreground-secondary">
    Last activity: <span className="text-foreground font-mono">{timeAgo}</span>
  </div>
</div>
```

**Interaction:**
- Hover: Border becomes cyan (`border-accent`)
- Click: Navigate to spoke detail view
- Status dot pulsing if "online" and active

#### Spoke Detail Profile View

When viewing a specific spoke (4.3 Spoke Detail), display the spoke profile information in the header or dedicated panel:

- **Machine Description** — Human-readable description of the machine/agent (e.g., "Work Laptop", "Personal Server")
- **Repos Managed** — List of repositories this spoke has access to (e.g., "eaglebyte/nexus", "my-private/project")
- **Integrations Available** — Connected tools and services (e.g., "GitHub", "Jira", "Linear", "Slack")
- **Last Sync** — Timestamp of last heartbeat or status check from the spoke

```jsx
<div className="border-b border-border bg-background-surface p-4">
  <h2 className="text-lg font-semibold text-foreground mb-3">{spokeName}</h2>
  <div className="grid grid-cols-2 gap-4 text-sm">
    <div>
      <div className="text-xs text-foreground-secondary">Description</div>
      <div className="text-foreground font-mono">{machineDescription}</div>
    </div>
    <div>
      <div className="text-xs text-foreground-secondary">Last Sync</div>
      <div className="text-foreground font-mono">{lastSyncTime}</div>
    </div>
  </div>
  <div className="mt-3">
    <div className="text-xs text-foreground-secondary mb-2">Repos</div>
    <div className="flex flex-wrap gap-2">
      {repos.map(repo => <span key={repo} className="px-2 py-1 bg-background-accent rounded text-xs font-mono text-foreground">{repo}</span>)}
    </div>
  </div>
  <div className="mt-3">
    <div className="text-xs text-foreground-secondary mb-2">Integrations</div>
    <div className="flex flex-wrap gap-2">
      {integrations.map(int => <span key={int} className="px-2 py-1 bg-background-accent rounded text-xs font-mono text-foreground">{int}</span>)}
    </div>
  </div>
</div>
```

---

### 3.3 Chat / Conversation Interface

For spoke conversations, use a terminal-inspired, not-chat-bubble layout.

#### Conversation Layout

Full viewport height, scrollable message area, input at bottom. Header shows spoke name (or "Hub" for hub-level conversations).

```jsx
<div className="flex flex-col h-screen bg-background">
  <header className="border-b border-border bg-background-surface px-6 py-4 flex items-center justify-between">
    <div>
      <h2 className="text-lg font-semibold text-foreground">Work Laptop</h2>
      <p className="text-sm text-foreground-secondary">Ask or assign work</p>
    </div>
    <button className="text-xs px-2 py-1 rounded bg-background-accent hover:bg-background-elevated text-foreground-secondary">
      Back to Conversations
    </button>
  </header>

  <div className="flex-1 overflow-y-auto space-y-4 p-6">
    {/* Messages render here */}
  </div>

  <div className="border-t border-border bg-background-surface p-6">
    {/* Input form */}
  </div>
</div>
```

**Features:**
- Conversation switching: Back button or sidebar navigation persists scroll position per conversation
- Real-time message streaming: Responses appear incrementally as Claude Code processes (via SignalR `ConversationMessageReceived`)
- Typing indicator: "spoke:work thinking..." appears while waiting for response
- Message count: Shown in sidebar for each conversation

#### Message Styling (Terminal-Inspired)

**User Message** (left-aligned, dark background, monospace timestamp)

```jsx
<div className="bg-background-elevated rounded border border-border-subtle p-4">
  <div className="flex justify-between items-start gap-2 mb-2">
    <span className="text-xs font-mono text-accent">user@nexus</span>
    <span className="text-xs text-foreground-muted">{timestamp}</span>
  </div>
  <div className="text-foreground leading-relaxed">
    {messageContent}
  </div>
</div>
```

**Spoke Response** (left-aligned, lighter background, agent prefix)

```jsx
<div className="bg-background-surface border border-border-subtle rounded p-4">
  <div className="flex items-start gap-2 mb-2">
    <span className="text-xs font-mono text-status-success">spoke:work</span>
    <span className="text-xs text-foreground-muted">{timestamp}</span>
  </div>
  <div className="text-foreground leading-relaxed whitespace-pre-wrap">
    {messageContent}
  </div>
</div>
```

**Code Block in Message**

```jsx
<div className="bg-background rounded font-mono text-sm text-foreground-secondary mt-2 p-3 border border-border-subtle overflow-x-auto">
  <pre>{codeSnippet}</pre>
</div>
```

**Typing Indicator**

```jsx
<div className="bg-background-surface border border-border-subtle rounded p-4">
  <span className="text-xs font-mono text-status-info">spoke:work</span>
  <div className="mt-2 flex items-center gap-1">
    <span className="inline-block w-2 h-2 bg-foreground-secondary rounded-full animate-bounce" />
    <span className="text-foreground-secondary text-sm">thinking...</span>
  </div>
</div>
```

#### Input Form

```jsx
<div className="flex gap-3">
  <input
    type="text"
    placeholder="Ask or assign work..."
    className="flex-1 bg-background-elevated border border-border rounded px-4 py-2 text-foreground placeholder-foreground-muted focus:outline-none focus:ring-1 focus:ring-accent"
  />
  <button className="px-4 py-2 bg-accent text-background-inverse rounded font-medium hover:bg-accent-secondary transition-colors">
    Send
  </button>
</div>
```

---

### 3.4 Job Stream / Terminal Output

Full-width terminal emulator aesthetic. Real-time output with scroll management.

#### Terminal Container

```jsx
<div className="w-full h-screen bg-background font-mono text-sm border border-border rounded">
  {/* Header Bar */}
  <div className="border-b border-border bg-background-surface px-4 py-2 flex justify-between items-center">
    <span className="text-foreground-secondary">Job #job-001 — Implementing PROJ-4521</span>
    <div className="flex gap-2">
      <button className="text-foreground-secondary hover:text-foreground text-xs px-2 py-1 hover:bg-background-accent rounded">
        Pause
      </button>
      <button className="text-foreground-secondary hover:text-foreground text-xs px-2 py-1 hover:bg-background-accent rounded">
        Copy
      </button>
      <button className="text-foreground-secondary hover:text-foreground text-xs px-2 py-1 hover:bg-background-accent rounded">
        ×
      </button>
    </div>
  </div>

  {/* Output Area */}
  <div className="overflow-y-auto flex-1 p-4 space-y-0">
    {outputLines.map((line, i) => (
      <div key={i} className="text-foreground whitespace-pre-wrap break-words">
        <span className="text-foreground-muted">$</span> {line}
      </div>
    ))}
    {isRunning && <span className="animate-pulse">▌</span>}
  </div>

  {/* Footer: Status */}
  <div className="border-t border-border bg-background-surface px-4 py-2 text-xs text-foreground-secondary">
    Running · Elapsed: {elapsedTime}
  </div>
</div>
```

#### ANSI Color Support

If output contains ANSI escape codes, map them to Nexus colors:

```javascript
const ansiColorMap = {
  '30': 'text-foreground-muted',      // Black → muted
  '31': 'text-status-error',          // Red → error
  '32': 'text-status-success',        // Green → success
  '33': 'text-status-warning',        // Yellow → warning
  '34': 'text-info',                  // Blue → info
  '36': 'text-accent',                // Cyan → accent
  '37': 'text-foreground',            // White → primary
};
```

---

### 3.5 Status Indicators & Badges

Consistent use of small badges to show state at a glance.

#### Online/Offline Badge

```jsx
<div className="inline-flex items-center gap-1 px-2 py-1 rounded text-xs font-mono">
  <div className={`w-1.5 h-1.5 rounded-full ${
    status === 'online' ? 'bg-status-success' : 'bg-status-error'
  }`} />
  <span className={status === 'online' ? 'text-status-success' : 'text-status-error'}>
    {status}
  </span>
</div>
```

#### Job Status Pill

```jsx
<div className={`inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-mono font-semibold ${
  {
    queued: 'bg-status-info/20 text-status-info',
    running: 'bg-status-warning/20 text-status-warning',
    completed: 'bg-status-success/20 text-status-success',
    failed: 'bg-status-error/20 text-status-error',
    'awaiting-approval': 'bg-accent/20 text-accent',
  }[status]
}`}>
  {status === 'running' && <div className="animate-spin w-1 h-1 rounded-full bg-current" />}
  {status}
</div>
```

#### Pulsing Live Indicator

```jsx
<div className="inline-flex items-center gap-1">
  <div className="w-2 h-2 bg-accent rounded-full animate-pulse" />
  <span className="text-xs text-foreground">LIVE</span>
</div>
```

---

### 3.6 Tables & Lists

For project lists, job history, and detailed tables.

#### Compact Table

```jsx
<table className="w-full text-sm">
  <thead>
    <tr className="border-b border-border-strong">
      <th className="text-left px-4 py-2 font-semibold text-foreground-secondary text-xs uppercase tracking-wide">
        Name
      </th>
      <th className="text-left px-4 py-2 font-semibold text-foreground-secondary text-xs uppercase tracking-wide">
        Status
      </th>
      <th className="text-left px-4 py-2 font-semibold text-foreground-secondary text-xs uppercase tracking-wide">
        Updated
      </th>
    </tr>
  </thead>
  <tbody>
    {items.map(item => (
      <tr
        key={item.id}
        className="border-b border-border hover:bg-background-accent transition-colors cursor-pointer"
      >
        <td className="px-4 py-2 font-mono text-foreground">{item.name}</td>
        <td className="px-4 py-2">
          <StatusBadge status={item.status} />
        </td>
        <td className="px-4 py-2 text-foreground-muted text-xs">{item.updatedAt}</td>
      </tr>
    ))}
  </tbody>
</table>
```

**Styling:**
- Header row: Gray background, uppercase labels, semibold
- Body rows: Light borders, hover highlight
- Data columns (IDs, timestamps): Monospace font
- Labels/names: Regular sans-serif

---

### 3.7 Forms & Inputs

Dark input fields with subtle borders, inline validation.

#### Text Input

```jsx
<input
  type="text"
  placeholder="Project name or ticket key..."
  className={`
    w-full bg-background-elevated border rounded px-3 py-2
    text-foreground placeholder-foreground-muted
    text-sm font-mono focus:outline-none focus:ring-1
    ${isError ? 'border-status-error focus:ring-status-error' : 'border-border focus:ring-accent'}
    transition-all
  `}
/>
```

#### Textarea

```jsx
<textarea
  rows={4}
  className={`
    w-full bg-background-elevated border border-border rounded px-3 py-2
    text-foreground placeholder-foreground-muted font-mono text-sm
    focus:outline-none focus:ring-1 focus:ring-accent
    resize-none
  `}
/>
```

#### Select / Dropdown

```jsx
<select
  className={`
    w-full bg-background-elevated border border-border rounded px-3 py-2
    text-foreground font-mono text-sm
    focus:outline-none focus:ring-1 focus:ring-accent
    cursor-pointer
  `}
>
  <option value="">Select an option</option>
  <option value="opt1">Option 1</option>
</select>
```

#### Approval Buttons

```jsx
<div className="flex gap-2">
  <button className="px-4 py-2 bg-status-success text-background-inverse rounded font-semibold hover:opacity-90 transition-opacity">
    Approve
  </button>
  <button className="px-4 py-2 bg-status-error text-background-inverse rounded font-semibold hover:opacity-90 transition-opacity">
    Reject
  </button>
</div>
```

---

## 4. Key Page Layouts

### 4.1 Dashboard (Home)

The landing page. Quick overview of all connected spokes, recent activity, and any pending approvals.

#### Desktop Layout

```
Header
├── Logo | Breadcrumb
├── Search bar
└── User menu

Body
├── Section: Spokes at a Glance
│   ├── 3-column grid of Spoke Status Cards
│   ├── Online/offline badges
│   ├── Job counts
│   └── Last activity timestamps
│
├── Section: Activity Feed
│   ├── Reversed chronological list
│   ├── Each item: timestamp (monospace), event type, spoke, summary
│   ├── Click to navigate to detail
│   └── Scrollable, max 15 items (load more or paginate)
│
└── Section: Pending Approvals (if any)
    ├── Yellow banner or card section
    ├── Plan review items
    ├── Approval buttons
    └── Quick preview of what needs approval
```

#### Mobile Layout

```
Header (56px)
├── Logo | Hamburger menu

Body (full width, bottom tab bar safe)
├── Section: Spokes (vertical stack)
│   └── One spoke per row, full width card
│
├── Section: Activity
│   └── Timeline items, compact
│
└── Pending Approvals
    └── Stack or carousel

Footer (56px fixed)
└── Bottom tab bar
```

#### Code Sketch

```jsx
export default function Dashboard() {
  return (
    <div className="flex flex-col min-h-screen bg-background">
      <Header />
      <main className="flex-1 max-w-6xl mx-auto w-full p-6">
        {/* Spokes */}
        <section>
          <h2 className="text-2xl font-semibold text-foreground mb-4">Spokes</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {spokes.map(spoke => <SpokeCard key={spoke.id} spoke={spoke} />)}
          </div>
        </section>

        {/* Activity Feed */}
        <section className="mt-8">
          <h2 className="text-2xl font-semibold text-foreground mb-4">Activity</h2>
          <div className="space-y-3 max-h-96 overflow-y-auto">
            {activities.map(activity => <ActivityItem key={activity.id} activity={activity} />)}
          </div>
        </section>

        {/* Approvals */}
        {pendingApprovals.length > 0 && (
          <section className="mt-8 border-l-4 border-status-warning bg-background-surface p-4 rounded">
            <h3 className="text-lg font-semibold text-status-warning mb-3">Awaiting Review</h3>
            <div className="space-y-2">
              {pendingApprovals.map(item => <ApprovalItem key={item.id} item={item} />)}
            </div>
          </section>
        )}
      </main>
      <BottomNav /> {/* Mobile only */}
    </div>
  );
}
```

---

### 4.2 Awaiting Input (Unified Queue)

Cross-spoke view of all items waiting for human-in-the-loop (HITL) attention. Shows **phase transitions awaiting user input**: plan approvals, pre-execution confirmations, post-execution reviews (including proposed PR comments), and blocker resolutions. Primary mobile view; prominent sidebar/tab link on desktop with pending count badge.

#### Desktop Layout

```
Header
├── Title: "Awaiting Input" + badge count
├── Filters: Gate type (All, Plan Review, Pre-Execution, Post-Execution, Question, PR Review)
└── Sort: By age (oldest first) or priority

Body
├── Queue list (sorted descending by creation time)
│   ├── Each item is a card (left-to-right):
│   │   ├── Spoke badge (color-coded, spoke name)
│   │   ├── Project/Ticket reference (e.g., "PROJ-4521")
│   │   ├── Gate type pill (color-coded: plan-review = info, pre-execution = warning, etc.)
│   │   ├── Time waiting (monospace, e.g., "2h 15m ago")
│   │   ├── Description (1–2 lines, ellipsis if long)
│   │   └── Quick-action button (Approve/Review/Respond, contextual)
│   │
│   └── Click item to navigate to detail view (project, job, or conversation)
│
└── Empty state: "No pending items — everything is running smoothly!"
```

**Item Card Structure (JSX Pattern)**

```jsx
<div className="flex items-center gap-4 p-4 border border-border rounded bg-card hover:border-accent transition-colors cursor-pointer">
  {/* Spoke Badge */}
  <div className="flex items-center gap-2 px-3 py-1 rounded-sm bg-background-accent text-xs font-mono text-foreground">
    <div className={`w-2 h-2 rounded-full ${spokeStatusColor}`} />
    {spokeName}
  </div>

  {/* Ticket / Project Reference */}
  <div className="font-mono text-sm text-foreground-secondary">
    {projectKey}
  </div>

  {/* Gate Type Pill */}
  <div className={`inline-flex items-center px-2 py-1 rounded text-xs font-mono font-semibold ${gateTypeColor}`}>
    {gateTypeLabel}
  </div>

  {/* Time Waiting */}
  <div className="text-xs text-foreground-muted font-mono">
    {timeAgo}
  </div>

  {/* Description */}
  <div className="flex-1 text-sm text-foreground truncate">
    {description}
  </div>

  {/* Quick Action Button */}
  <button className={`px-3 py-2 rounded text-xs font-semibold transition-colors ${actionButtonColor}`}>
    {actionLabel}
  </button>
</div>
```

#### Mobile Layout

```
Header (sticky)
├── Title: "Awaiting Input" + badge
└── Filters (icon/dropdown)

Body
├── Card stack (full width)
│   ├── Each card: vertical layout
│   │   ├── Row 1: Spoke badge + Gate type pill (flex between)
│   │   ├── Row 2: Ticket key + Time waiting
│   │   ├── Row 3: Description (2 lines, truncated)
│   │   └── Row 4: Full-width action button
│   │
│   └── Tap to navigate to detail
│
└── Empty state: "All clear!"
```

**Mobile Card Structure (JSX)**

```jsx
<div className="p-3 border border-border rounded bg-card space-y-3">
  {/* Row 1: Spoke + Gate */}
  <div className="flex items-center justify-between gap-2">
    <div className="flex items-center gap-2 px-2 py-1 rounded-sm bg-background-accent text-xs font-mono">
      <div className={`w-1.5 h-1.5 rounded-full ${spokeStatusColor}`} />
      {spokeName}
    </div>
    <div className={`text-xs font-mono font-semibold rounded px-2 py-1 ${gateTypeColor}`}>
      {gateTypeLabel}
    </div>
  </div>

  {/* Row 2: Ticket + Time */}
  <div className="flex items-center justify-between text-xs">
    <span className="font-mono text-foreground-secondary">{projectKey}</span>
    <span className="text-foreground-muted">{timeAgo}</span>
  </div>

  {/* Row 3: Description */}
  <p className="text-sm text-foreground line-clamp-2">
    {description}
  </p>

  {/* Row 4: Action */}
  <button className={`w-full px-3 py-2 rounded text-xs font-semibold transition-colors ${actionButtonColor}`}>
    {actionLabel}
  </button>
</div>
```

#### Gate Type Color Scheme

| Gate Type | Pill Color | Meaning |
|---|---|---|
| **Plan Review** | `bg-status-info/20 text-status-info` | Implementation plan waiting for approval (between plan and implement phases) |
| **Pre-Execution** | `bg-status-warning/20 text-status-warning` | Job queued, waiting for final go-ahead before starting work |
| **Post-Execution** | `bg-accent/20 text-accent` | Job completed, branch/PR created (between implement and PR phases); awaits approval to post PR comments |
| **Spoke Question** | `bg-status-warning/20 text-status-warning` | Spoke hit a blocker during execution, needs guidance |
| **PR Review** | `bg-status-info/20 text-status-info` | Proposed PR comments ready for user review before posting |

**Important:** Approval gates occur **between job phases** (plan → implement → PR), not on individual tool calls. The "Awaiting Input" queue surfaces phase transitions waiting for user input or decision.

#### Navigation & Actions

- **Click item** → Navigate to context view (project detail, job stream, or conversation)
- **Action button (Approve)** → Shows approval confirmation modal or inline approval (for plan reviews and pre-execution gates)
- **Action button (Review)** → Opens detail view with plan/diff for inspection; for PR Review gates, shows proposed PR comments for user approval before posting
- **Action button (Respond)** → Opens conversation with spoke in a modal or new view (for blocker questions)
- **Swipe left (mobile)** → Quick action menu (Approve, Dismiss, Snooze)

**PR Comment Review Flow:** When a "PR Review" gate appears, clicking the item or "Review" button displays the list of proposed PR comments. User can approve them as-is, edit individual comments before posting, or reject them entirely.

---

### 4.3 Spoke Detail

Conversational interface with the spoke. Chat on the left/main, projects and active job sidebar on desktop; tabbed on mobile.

#### Desktop Layout

```
Header: Spoke name + status badge + actions

Body
├─ Main: Conversation panel
│  ├─ Message history (scrollable)
│  ├─ Input form (bottom)
│  └─ Auto-scroll to latest (unless manually scrolled up)
│
└─ Right sidebar (240px, or modal on <1024px)
   ├─ Active Jobs section
   │  ├─ Current running job (if any), full-width status
   │  └─ Queued jobs list
   │
   └─ Projects section
      ├─ List of active projects
      ├─ Click to view detail
      └─ Badge: status, job count
```

#### Mobile Layout

```
Header: Spoke name + status + menu button

Body
├─ Tabbed interface
│  ├─ Chat tab (conversation, input)
│  ├─ Jobs tab (active/queued jobs list)
│  └─ Projects tab (projects list)
│
└─ Tab panels swap content area
```

#### Code Sketch

```jsx
export default function SpokeDetail({ spokeId }) {
  const [tab, setTab] = useState('chat');

  return (
    <div className="flex flex-col min-h-screen bg-background">
      <SpokeHeader spokeId={spokeId} />

      {/* Desktop: side-by-side; Mobile: tabbed */}
      <div className="flex flex-1">
        <main className="flex-1">
          {tab === 'chat' && <ConversationPanel spokeId={spokeId} />}
          {tab === 'jobs' && <JobsPanel spokeId={spokeId} />}
          {tab === 'projects' && <ProjectsPanel spokeId={spokeId} />}
        </main>

        {/* Right sidebar on desktop only */}
        <aside className="hidden lg:block w-64 border-l border-border bg-background-surface overflow-y-auto">
          <JobsPanel spokeId={spokeId} compact />
          <ProjectsPanel spokeId={spokeId} compact />
        </aside>
      </div>

      {/* Tab bar on mobile */}
      <TabBar tab={tab} setTab={setTab} />
    </div>
  );
}
```

---

### 4.4 Project Detail

Ticket info, current plan, job history, status progression.

#### Layout

```
Header
├─ Ticket key + summary
├─ External link to Jira
└─ Status badge

Body (tabbed or sectioned)
├─ Tab 1: Overview
│  ├─ Ticket description
│  ├─ Acceptance criteria (if present)
│  ├─ Current status indicator
│  └─ Related tickets (if any)
│
├─ Tab 2: Plan
│  ├─ Agent-generated implementation plan
│  ├─ Edit button (if not yet approved)
│  ├─ Approve / Reject buttons
│  └─ Version history (previous plans)
│
├─ Tab 3: Jobs
│  ├─ Job history timeline
│  ├─ Each job: status, date, summary, link to output
│  └─ Ability to re-run or inspect
│
└─ Tab 4: Context
   ├─ Assembled context (memory excerpts, ticket, etc.)
   ├─ Last updated timestamp
   └─ Refresh button
```

---

### 4.5 Job Stream

Live terminal output view. Modal or full-screen.

#### Layout

```
Header Bar
├─ Job ID, project key, title
├─ Status: Running / Completed / Failed
└─ Controls: Pause scroll, Copy output, Close

Output Area (monospace, scrollable)
├─ $ prompt lines
├─ Colored output if ANSI supported
├─ Auto-scroll by default
├─ Manual scroll locks auto-scroll

Footer
├─ Job status summary
├─ Elapsed time
└─ Estimated time remaining (if available)
```

---

## 5. Interaction Patterns

### 5.1 Real-Time Updates

**Spoke Status Change** (Online → Offline)

- Background color smoothly transitions on status change
- Border gets a pulse animation (2–3 pulses, then stop)
- Doesn't reload the page; uses SignalR event

```css
@keyframes statusPulse {
  0% { border-color: currentColor; }
  50% { border-color: transparent; }
  100% { border-color: currentColor; }
}

.status-changing {
  animation: statusPulse 1s ease-in-out;
}
```

**New Job Appears**

- Item slides in from top of list with a subtle fade
- 150ms ease-out transition
- Status badge updates in real-time as job progresses

```jsx
<motion.div
  initial={{ opacity: 0, y: -10 }}
  animate={{ opacity: 1, y: 0 }}
  transition={{ duration: 0.15 }}
>
  {/* Job item */}
</motion.div>
```

**Live Output Streaming**

- Lines append to terminal output without reflow
- Scroll auto-follows unless user manually scrolls up
- No jarring line-jumps; smooth continuous flow

### 5.2 Responsive Behavior

| Breakpoint | Screen Width | Sidebar | Tabs | Bottom Nav | Max Content Width |
|---|---|---|---|---|---|
| Mobile | <768px | Hidden (drawer) | Bottom tabs | Yes (fixed) | 100% |
| Tablet | 768–1023px | Collapsible hamburger | Top tabs | Optional | 100% |
| Desktop | ≥1024px | Left sidebar (240px) | None (side panel) | No | 1200px |

**Example: Sidebar Collapse**

```jsx
const [sidebarOpen, setSidebarOpen] = useState(false);

return (
  <div className="flex">
    {/* Mobile: Overlay drawer */}
    {sidebarOpen && <Sidebar onClose={() => setSidebarOpen(false)} className="md:hidden" />}

    {/* Desktop: Fixed sidebar */}
    <Sidebar className="hidden md:block w-60 fixed left-0 top-14 bottom-0 overflow-y-auto" />

    {/* Main content, adjusted for sidebar on desktop */}
    <main className="flex-1 md:ml-60">
      <button onClick={() => setSidebarOpen(!sidebarOpen)} className="md:hidden p-2">
        ☰
      </button>
      {/* Content */}
    </main>
  </div>
);
```

### 5.3 Keyboard Shortcuts

For power users. Opt-in, not required.

| Shortcut | Action |
|---|---|
| `Cmd+K` / `Ctrl+K` | Open command palette (search projects, jobs, spokes) |
| `Cmd+/` | Show help / keyboard shortcuts |
| `←` / `→` | Navigate between tabs (when tabbed panel is focused) |
| `Escape` | Close modals, cancel ongoing actions |
| `Enter` | Submit forms, approve actions (when button is focused) |

```jsx
import { useEffect } from 'react';

export function KeyboardShortcuts() {
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        openCommandPalette();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);
}
```

### 5.4 Notifications

Toast notifications for key events. Subtle, non-intrusive.

```jsx
<div className="fixed bottom-20 md:bottom-4 right-4 space-y-2 z-50">
  {notifications.map(notif => (
    <div
      key={notif.id}
      className={`px-4 py-2 rounded border text-sm font-mono ${
        {
          success: 'bg-status-success/20 text-status-success border-status-success',
          error: 'bg-status-error/20 text-status-error border-status-error',
          info: 'bg-status-info/20 text-status-info border-status-info',
        }[notif.type]
      }`}
    >
      {notif.message}
    </div>
  ))}
</div>
```

---

## 6. Animation & Motion

Keep animations functional, not decorative. Duration: 100–200ms.

### Approved Animations

| Element | Animation | Duration | Purpose |
|---|---|---|---|
| Button hover | Color/opacity transition | 150ms | Visual feedback |
| Modal enter | Fade in, slight scale | 150ms | Entrance feel |
| Sidebar slide | Transform + fade | 200ms | Navigation feedback |
| Loading spinner | Rotate, minimal | Continuous | Progress indication |
| Toast fade out | Opacity, slide out | 200ms | Dismissal |

### Avoid

- Bouncing or overshoot (too playful for a command center)
- 3D transforms or perspective (unnecessary complexity)
- Parallax scrolling (can cause dizziness during long sessions)
- Continuous animations on background elements (distraction + battery drain)

### Example: Smooth Transitions

```css
.transition-fast {
  transition: all 150ms ease-out;
}

.transition-normal {
  transition: all 200ms ease-out;
}

button:hover {
  @apply transition-fast;
  /* Color, opacity, transform changes are smooth */
}
```

---

## 7. Accessibility

### Contrast Ratios

All text meets WCAG AA minimum (4.5:1 for small text, 3:1 for large text).

- Primary text (#f5f5f5) on base background (#0a0a0a): 18:1 ✓
- Secondary text (#a0a0a0) on base: 8:1 ✓
- Accent (#00d9ff) on base: 10:1 ✓
- Status green on base: 8:1 ✓

### Focus Indicators

Every interactive element must have a visible focus ring. Default: 1px ring of accent color.

```css
input:focus, button:focus, a:focus {
  outline: 1px solid #00d9ff;
  outline-offset: 2px;
}

/* Or with shadcn: */
.focus-visible:ring-1 .focus-visible:ring-accent
```

### Semantic HTML

- Use `<button>` for buttons, not `<div>` with click handlers
- Use `<nav>` for navigation
- Use `<table>` for tabular data
- Use `<label>` associated with form inputs via `htmlFor`
- Use `<h1>`, `<h2>`, etc., in proper hierarchy

### Screen Reader Support

- Images: `alt` text or `aria-hidden="true"` if decorative
- Icons (status dots, badges): Pair with text or `aria-label`
- Live regions: Use `aria-live="polite"` for real-time updates (new jobs, status changes)
- Tab order: Ensure keyboard tab order matches visual order

```jsx
<div aria-live="polite">
  New job: PROJ-4521 started running
</div>
```

### Keyboard Navigability

- Tab through all interactive elements
- Shift+Tab to go backward
- Enter to activate buttons
- Arrow keys for menus/tabs
- Escape to close modals

---

## 8. shadcn/ui Theme Configuration

### CSS Variables in `globals.css`

```css
@layer base {
  :root {
    /* Background layers */
    --background: 0 0% 4%;             /* #0a0a0a */
    --card: 0 0% 8%;                   /* #141414 */
    --popover: 0 0% 10%;               /* #1a1a1a */

    /* Foreground text */
    --foreground: 0 0% 96%;            /* #f5f5f5 */
    --card-foreground: 0 0% 96%;
    --popover-foreground: 0 0% 96%;

    /* Accent (cyan) */
    --accent: 187 100% 50%;            /* #00d9ff */
    --accent-foreground: 0 0% 4%;      /* #0a0a0a */

    /* Muted (secondary) */
    --muted: 0 0% 42%;                 /* #a0a0a0 */
    --muted-foreground: 0 0% 42%;

    /* Destructive (error) */
    --destructive: 0 84% 60%;          /* #ef4444 */
    --destructive-foreground: 0 0% 96%;

    /* Border */
    --border: 0 0% 20%;                /* #333333 */
    --input: 0 0% 20%;

    /* Focus ring */
    --ring: 187 100% 50%;              /* Accent cyan */

    /* Radius */
    --radius: 0.375rem;                /* 6px */
  }

  * {
    @apply border-border;
  }
}
```

### Tailwind Config Extensions

```typescript
// tailwind.config.ts
import type { Config } from 'tailwindcss';

const config: Config = {
  darkMode: 'class', // Always on in Nexus
  theme: {
    extend: {
      colors: {
        background: {
          DEFAULT: '#0a0a0a',
          surface: '#141414',
          elevated: '#1a1a1a',
          accent: '#242424',
        },
        foreground: {
          DEFAULT: '#f5f5f5',
          secondary: '#a0a0a0',
          muted: '#6b6b6b',
          inverse: '#0a0a0a',
        },
        accent: {
          DEFAULT: '#00d9ff',
          secondary: '#0099cc',
          muted: '#004466',
        },
        status: {
          success: '#10b981',
          warning: '#f59e0b',
          error: '#ef4444',
          info: '#3b82f6',
        },
        border: {
          DEFAULT: '#333333',
          subtle: '#252525',
          strong: '#404040',
        },
      },
      fontFamily: {
        sans: ['var(--font-geist-sans)', 'system-ui', 'sans-serif'],
        mono: ['var(--font-geist-mono)', 'ui-monospace', 'monospace'],
      },
      fontSize: {
        xs: '0.75rem',     /* 12px */
        sm: '0.875rem',    /* 14px */
        base: '1rem',      /* 16px */
        lg: '1.125rem',    /* 18px */
        xl: '1.25rem',     /* 20px */
        '2xl': '1.5rem',   /* 24px */
        '3xl': '2rem',     /* 32px */
      },
      spacing: {
        // Tailwind already uses 4px base units (p-1 = 4px, p-2 = 8px, etc.)
        // No changes needed; the default scale is correct
      },
      animation: {
        'pulse-soft': 'pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
      },
    },
  },
  plugins: [require('tailwindcss-animate')],
};

export default config;
```

### shadcn/ui Component Customizations

When scaffolding shadcn components, use these overrides to match Nexus styling:

**Button (Primary / CTA)**

```typescript
// Use accent color as primary, background-elevated as background
export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = 'default', ...props }, ref) => (
    <button
      ref={ref}
      className={cn(
        'px-4 py-2 rounded text-sm font-medium transition-colors focus-visible:ring-1 focus-visible:ring-accent',
        {
          'bg-accent text-background-inverse hover:bg-accent-secondary': variant === 'default',
          'bg-transparent border border-border text-foreground hover:bg-background-accent': variant === 'outline',
          'bg-background-accent text-foreground-secondary hover:bg-background-accent/80': variant === 'ghost',
        },
        className
      )}
      {...props}
    />
  )
);
```

**Card**

```typescript
export const Card = React.forwardRef<
  HTMLDivElement,
  React.HTMLAttributes<HTMLDivElement>
>(({ className, ...props }, ref) => (
  <div
    ref={ref}
    className={cn(
      'rounded-md border border-border bg-card p-4',
      className
    )}
    {...props}
  />
));
```

**Input**

```typescript
export const Input = React.forwardRef<
  HTMLInputElement,
  React.InputHTMLAttributes<HTMLInputElement>
>(({ className, ...props }, ref) => (
  <input
    ref={ref}
    className={cn(
      'w-full bg-background-elevated border border-border rounded px-3 py-2 text-foreground placeholder-foreground-muted text-sm focus:outline-none focus:ring-1 focus:ring-accent transition-all',
      className
    )}
    {...props}
  />
));
```

---

## 9. Implementation Checklist

Use this when building the UI to ensure consistency:

- [ ] All backgrounds use the color system (base, surface, elevated, accent)
- [ ] All text uses proper hierarchy (H1–H4, Body LG/Base/SM, Caption)
- [ ] Monospace font applied to: timestamps, IDs, status values, terminal output, job IDs
- [ ] All interactive elements have focus rings (outline or ring)
- [ ] Hover states defined for buttons, cards, rows
- [ ] Border colors use border palette (not arbitrary grays)
- [ ] Spacing uses 4px multiples (p-2, p-4, gap-3, etc.)
- [ ] Cards have 1px border (subtle, not filled backgrounds)
- [ ] Status indicators use correct colors (green, amber, red, blue)
- [ ] Forms have inline labels or placeholders
- [ ] Mobile breakpoints tested (<768px, 768–1024px, >1024px)
- [ ] Dark mode working everywhere (no light backgrounds leaking through)
- [ ] Contrast ratios verified (use WebAIM tool)
- [ ] Keyboard navigation tested (Tab, Shift+Tab, Enter, Escape)
- [ ] Real-time updates don't cause layout shifts
- [ ] Animations smooth and <200ms (no jank on lower-end devices)

---

## 10. References & Tools

- **Tailwind CSS:** https://tailwindcss.com (utility-first styling)
- **shadcn/ui:** https://ui.shadcn.com (headless component library)
- **Next.js:** https://nextjs.org (React framework, server-side rendering)
- **Geist Font:** https://vercel.com/font (system font, available via Vercel)
- **WebAIM Contrast Checker:** https://webaim.org/resources/contrastchecker/
- **WCAG 2.1 AA:** https://www.w3.org/WAI/WCAG21/quickref/ (accessibility standard)
- **Framer Motion:** https://www.framer.com/motion (animation library for React)

---

## 11. Design System Tokens

For ease of reference and consistency, here's a JSON representation of the full design system:

```json
{
  "colors": {
    "background": {
      "base": "#0a0a0a",
      "surface": "#141414",
      "elevated": "#1a1a1a",
      "accent": "#242424"
    },
    "foreground": {
      "primary": "#f5f5f5",
      "secondary": "#a0a0a0",
      "muted": "#6b6b6b",
      "inverse": "#0a0a0a"
    },
    "accent": {
      "primary": "#00d9ff",
      "secondary": "#0099cc",
      "muted": "#004466"
    },
    "status": {
      "success": "#10b981",
      "warning": "#f59e0b",
      "error": "#ef4444",
      "info": "#3b82f6"
    },
    "border": {
      "default": "#333333",
      "subtle": "#252525",
      "strong": "#404040"
    }
  },
  "typography": {
    "fontFamily": {
      "sans": "Geist Sans, Inter, system-ui, sans-serif",
      "mono": "Geist Mono, JetBrains Mono, ui-monospace, monospace"
    },
    "fontSize": {
      "h1": "32px (2rem)",
      "h2": "24px (1.5rem)",
      "h3": "18px (1.125rem)",
      "h4": "16px (1rem)",
      "bodyLg": "16px (1rem)",
      "body": "14px (0.875rem)",
      "bodySm": "12px (0.75rem)",
      "code": "12px (0.75rem)"
    },
    "fontWeight": {
      "light": 300,
      "normal": 400,
      "medium": 500,
      "semibold": 600,
      "bold": 700
    }
  },
  "spacing": {
    "base": "4px",
    "xs": "2px",
    "sm": "8px",
    "md": "12px",
    "lg": "16px",
    "xl": "24px",
    "2xl": "32px"
  },
  "layout": {
    "sidebarWidth": "240px",
    "headerHeight": "56px",
    "maxContentWidth": "1200px"
  },
  "animation": {
    "fast": "150ms ease-out",
    "normal": "200ms ease-out"
  }
}
```

---

## Closing Notes

This design system is **opinionated and intentional**. Every choice supports the goal of creating a personal, efficient command center for an engineer juggling multiple projects. The dark theme, terminal-inspired language, information density, and responsive patterns all serve that mission.

When implementing, prioritize:

1. **Consistency** — Use the color system, spacing rules, and typography scale everywhere.
2. **Accessibility** — High contrast, clear focus states, semantic HTML.
3. **Performance** — Avoid unnecessary re-renders, keep animations smooth, stream real-time data efficiently.
4. **Clarity** — Every UI element should have a clear purpose and obvious interaction.

The system is extensible — add new components as needed, but follow the patterns established here.

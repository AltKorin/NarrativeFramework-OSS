export type Actor = { id: string; title: string; kind: string };
export type Effect = {
  type: string;
  targetId: string;
  value: number;
  actorId?: string;
  otherActorId?: string;
};
export type Condition = {
  scope: string;
  key: string;
  comparison: string;
  value: number;
  actorId?: string;
};
export type Choice = { text: string; outcomes: Effect[] };
export type Blocker = {
  code: string;
  key?: string;
  day?: number;
  current?: number;
  value?: number;
  comparison?: string;
  actorId?: string;
};
export type Event = {
  id: string;
  title: string;
  description: string;
  MinTimeUnit: number;
  MaxTimeUnit: number;
  requiredFlags: string[];
  forbiddenFlags: string[];
  InvolvedActors: string[];
  conditions: Condition[];
  choices: Choice[];
  blockers: Blocker[];
};
export type History = {
  day: number;
  action: string;
  title?: string;
  choice?: string;
  key?: string;
  before?: unknown;
  after?: unknown;
  actorId?: string;
  outcomes?: Effect[];
};
export type Runtime = {
  version: number;
  day: number;
  flags: string[];
  resources: Record<string, number>;
  variables: Record<string, number>;
  bools: Record<string, boolean>;
  actors: Record<string, Record<string, number>>;
  relationships: Record<string, Record<string, Record<string, number>>>;
  events: Event[];
  history: History[];
};
export type Request = (
  path: string,
  method?: string,
  body?: unknown,
) => Promise<any>;

export type CampaignView = "run" | "timeline" | "actors" | "materials";

import { describe, expect, it } from 'vitest';
import { economy, fresh, type Reels, type State } from '../src/core/model';
import { payout } from '../src/slot/spin';
import { applyPending, reserve } from '../src/progression/transition';
import { GameStore, keys, type Storage, validate } from '../src/platform/save';
class MemoryStorage implements Storage {
  values = new Map<string, string>(); writes = 0; failAt = 0;
  async get(key: string) { return this.values.get(key) ?? null; }
  async set(key: string, value: string) { this.writes++; if (this.writes === this.failAt) throw new Error('simulated storage interruption'); this.values.set(key, value); }
}
function step(s: State, command: Parameters<typeof reserve>[1]): State { return applyPending(reserve(s, command, s.sequence + 1)); }
describe('independent economic outcomes', () => {
  it('enumerates all 64 outcomes, exclusive payouts and expected reward', () => {
    const counts: Record<number, number> = { 23: 0, 8: 0, 3: 0 }; let total = 0;
    for (const a of economy.symbols) for (const b of economy.symbols) for (const c of economy.symbols) {
      const expected = a === b && b === c ? 23 : a === b || a === c || b === c ? 8 : 3;
      const actual = payout([a,b,c] as Reels); expect(actual).toBe(expected); counts[actual]++; total += actual;
    }
    expect(counts).toEqual({ 23: 4, 8: 36, 3: 24 }); expect(total / 64).toBe(7.0625);
  });
  it('preview is free; one normal spin after explicit selection repairs once and keeps surplus', () => {
    let s = fresh(1); expect(s.coins).toBe(37); expect(s.selected).toBeNull();
    s = step(s, {kind:'select',target:1}); expect(s.coins).toBe(37); expect(s.world.house).toBe(0);
    s = step(s, {kind:'spin'}); expect(s.world.house).toBe(1); expect([0,5,20]).toContain(s.coins); expect(s.selected).toBeNull();
    const before = s.coins; s = step(s, {kind:'spin'}); expect(s.world.house).toBe(1); expect(s.coins).toBeGreaterThan(before);
  });
  it('selecting an affordable project buys once; exact funds and surplus both work', () => {
    for (const funds of [40,55]) { const s = step({...fresh(4),coins:funds},{kind:'select',target:1}); expect(s.coins).toBe(funds-40); expect(s.world.house).toBe(1); expect(s.selected).toBeNull(); }
  });
  it('second project requires explicit selection, costs 600 and ends at stage 2', () => {
    let s = {...fresh(5),coins:1000}; s = step(s,{kind:'select',target:1}); expect(s.world.house).toBe(1);
    s = step(s,{kind:'select',target:2}); expect(s.coins).toBe(360); expect(s.world.house).toBe(2); expect(s.selected).toBeNull();
    expect(() => step(s,{kind:'select',target:2})).toThrow();
  });
  it('stale commands stay inert without a receipt history', () => {
    let s = fresh(123); for (let i=0;i<200;i++) s = step(s,{kind:'spin'});
    expect(reserve(s,{kind:'spin'},1)).toBe(s); expect(applyPending(s)).toBe(s);
    expect(() => reserve(s,{kind:'spin'},202)).toThrow('gap');
  });
});
describe('serialized saves and interruption recovery', () => {
  it('persists the initial grant once across reloads', async () => { const db = new MemoryStorage(); const store = new GameStore(db); await store.open(3); await store.execute({kind:'spin'},1); const next = new GameStore(db); expect((await next.open(999)).coins).toBe(store.state.coins); });
  it('duplicate rapid commands credit once', async () => { const db = new MemoryStorage(); const store = new GameStore(db); await store.open(3); const states = await Promise.all([store.execute({kind:'spin'},1),store.execute({kind:'spin'},1)]); expect(states[0]).toEqual(states[1]); expect(store.state.sequence).toBe(1); });
  it.each([2,3,4,5])('recovers failure at write %i without reroll or double reward', async failAt => {
    const db = new MemoryStorage(); const store = new GameStore(db); await store.open(222);
    const expected = step(fresh(222),{kind:'spin'}); db.failAt = failAt;
    await expect(store.execute({kind:'spin'},1)).rejects.toThrow('interruption');
    await expect(store.execute({kind:'spin'},1)).rejects.toThrow('остановлена');
    const recovery = new GameStore(db); const actual = await recovery.open(999);
    // Before primary reservation, command was not accepted. After it, recover its exact result.
    if (failAt <= 3) expect(actual).toEqual(fresh(222)); else expect(actual).toEqual(expected);
    expect((await recovery.execute({kind:'spin'},1)).coins).toBe(expected.coins);
  });
  it('recovers purchase reserved before termination', async () => {
    const db = new MemoryStorage(); const original = {...fresh(7),coins:60}; const pending = reserve(original,{kind:'select',target:1},1);
    db.values.set(keys.primary,JSON.stringify(pending)); const store = new GameStore(db); expect((await store.open(88)).coins).toBe(20); expect(store.state.world.house).toBe(1);
    expect((await store.execute({kind:'select',target:1},1)).coins).toBe(20);
  });
  it('committed state remains applied when effects never run', async () => { const db = new MemoryStorage(); const store = new GameStore(db); await store.open(42); await store.execute({kind:'spin'},1); const recovery = new GameStore(db); await recovery.open(333); expect(await recovery.execute({kind:'spin'},1)).toEqual(store.state); });
  it.each([
    (s: State) => ({...s,schemaVersion:2}), (s: State) => ({...s,coins:-1}), (s: State) => ({...s,revision:20}),
    (s: State) => ({...s,pending:{kind:'spin',sequence:1,symbols:['potato','potato','potato'],seedAfter:10},revision:1}),
    (s: State) => ({...s,selected:'house:2'}), (s: State) => ({...s,lastSpin:undefined}), (s: State) => ({...s,pending:undefined})
  ])('rejects incompatible or malformed state without mutation', async corrupt => {
    const db = new MemoryStorage(); const raw = JSON.stringify(corrupt(fresh(9))); db.values.set(keys.primary,raw);
    await expect(new GameStore(db).open(3)).rejects.toThrow(); expect(db.values.get(keys.primary)).toBe(raw); expect(db.writes).toBe(0);
  });
  it('backup recovery is explicit and preserves the corrupt primary', async () => {
    const db = new MemoryStorage(); db.values.set(keys.primary,'bad'); db.values.set(keys.backup,JSON.stringify(fresh(4)));
    const store = new GameStore(db); await expect(store.open(99)).rejects.toMatchObject({backupAvailable:true});
    expect(db.values.get(keys.primary)).toBe('bad'); await store.restoreBackup(); expect(db.values.get('jtn.recovery-original.v1')).toBe('bad'); expect(validate(db.values.get(keys.primary)!)).toEqual(fresh(4));
  });
});

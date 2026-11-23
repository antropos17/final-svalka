import { Capacitor } from '@capacitor/core';
import { Preferences } from '@capacitor/preferences';
import { economy, fresh, type State } from '../core/model';
import { draw, payout } from '../slot/spin';
import { applyPending, reserve, type Command } from '../progression/transition';
export interface Storage { get(key: string): Promise<string | null>; set(key: string, value: string): Promise<void> }
export const keys = { primary: 'jtn.save.v1', backup: 'jtn.backup.v1' };
export class PlatformStorage implements Storage {
  async get(key: string) { return Capacitor.isNativePlatform() ? (await Preferences.get({ key })).value : localStorage.getItem(key); }
  async set(key: string, value: string) { if (Capacitor.isNativePlatform()) await Preferences.set({ key, value }); else localStorage.setItem(key, value); }
}
const integer = (n: unknown): n is number => Number.isSafeInteger(n) && Number(n) >= 0;
export function validate(raw: string): State {
  const s = JSON.parse(raw) as State;
  if (!s || s.schemaVersion !== 1 || s.contentVersion !== economy.contentVersion || !integer(s.revision) || !integer(s.sequence) || !integer(s.coins) || !s.world || !integer(s.world.house) || s.world.house > 2 || !integer(s.seed) || s.seed < 1 || s.seed > 0xffffffff) throw new Error('Invalid save format or version');
  if (s.selected !== null && (s.world.house >= 2 || s.selected !== `house:${s.world.house + 1}` || s.coins >= economy.prices[s.world.house])) throw new Error('Invalid selected project');
  const validReels = (v: unknown): boolean => Array.isArray(v) && v.length === 3 && v.every(x => economy.symbols.includes(x));
  if (s.lastSpin !== null && (!s.lastSpin || !integer(s.lastSpin.sequence) || s.lastSpin.sequence < 1 || s.lastSpin.sequence > s.sequence || !validReels(s.lastSpin.symbols) || s.lastSpin.reward !== payout(s.lastSpin.symbols))) throw new Error('Invalid spin receipt');
  if (s.pending !== null) {
    const p = s.pending;
    if (!p || p.sequence !== s.sequence + 1) throw new Error('Invalid pending sequence');
    if (p.kind === 'spin') {
      const expected = draw(s.seed);
      if (!validReels(p.symbols) || JSON.stringify(p.symbols) !== JSON.stringify(expected.symbols) || p.seedAfter !== expected.seedAfter) throw new Error('Invalid frozen result');
    } else if (p.kind !== 'select' || p.target !== s.world.house + 1 || p.target > 2) throw new Error('Invalid pending project');
  }
  // Every command uses exactly two snapshots; reserved state is the odd revision.
  if (s.revision !== s.sequence * 2 + (s.pending ? 1 : 0)) throw new Error('Invalid revision sequence');
  return s;
}
export class SaveError extends Error { constructor(message: string, public backupAvailable = false) { super(message); } }
export class GameStore {
  state!: State;
  private previous: string | null = null;
  private queue: Promise<unknown> = Promise.resolve();
  private blocked = false;
  constructor(private storage: Storage) {}
  private async write(state: State) {
    const raw = JSON.stringify(state); validate(raw);
    if (this.previous !== null) await this.storage.set(keys.backup, this.previous);
    await this.storage.set(keys.primary, raw);
    this.previous = raw; this.state = state;
  }
  async open(seed: number): Promise<State> {
    try {
      const raw = await this.storage.get(keys.primary);
      const backup = await this.storage.get(keys.backup);
      if (raw === null) {
        if (backup !== null) throw new SaveError('Основная запись отсутствует. Доступна резервная копия.', true);
        await this.write(fresh(seed));
      } else {
        try { this.state = validate(raw); this.previous = raw; }
        catch { let available = false; try { if (backup) { validate(backup); available = true; } } catch { /* Preserve both original candidates. */ }
          throw new SaveError('Сохранение требует восстановления. Исходные записи сохранены.', available); }
        if (this.state.pending) await this.write(applyPending(this.state));
      }
      this.blocked = false; return this.state;
    } catch (error) { this.blocked = true; throw error; }
  }
  async restoreBackup(): Promise<State> {
    const raw = await this.storage.get(keys.backup);
    if (!raw) throw new SaveError('Нет резервной копии.');
    const valid = validate(raw);
    // Preserve an uncertain primary before the owner's explicit rollback.
    const original = await this.storage.get(keys.primary);
    if (original) await this.storage.set('jtn.recovery-original.v1', original);
    this.previous = null; await this.write(valid);
    if (valid.pending) await this.write(applyPending(valid));
    this.blocked = false; return this.state;
  }
  execute(command: Command, sequence: number): Promise<State> {
    const work = this.queue.then(async () => {
      if (this.blocked) throw new SaveError('Запись остановлена. Перезапустите игру.');
      const pending = reserve(this.state, command, sequence);
      if (pending === this.state) return this.state;
      try { await this.write(pending); await this.write(applyPending(pending)); return this.state; }
      catch (error) { this.blocked = true; throw error; }
    });
    this.queue = work.catch(() => undefined); return work;
  }
}

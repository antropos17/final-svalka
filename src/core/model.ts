import economy from '../../config/economy.json';
export { economy };
export type SymbolId = 'potato' | 'box' | 'trash' | 'pigeon';
export type Reels = [SymbolId, SymbolId, SymbolId];
export interface Spin { sequence: number; symbols: Reels; reward: number }
export type Pending = { sequence: number; kind: 'spin'; symbols: Reels; seedAfter: number } | { sequence: number; kind: 'select'; target: 1 | 2 };
export interface State {
  schemaVersion: 1; contentVersion: number; revision: number; sequence: number;
  coins: number; world: { house: number }; selected: string | null; seed: number;
  lastSpin: Spin | null; pending: Pending | null;
}
export function fresh(seed: number): State {
  return { schemaVersion: 1, contentVersion: economy.contentVersion, revision: 0, sequence: 0, coins: economy.initialCoins, world: { house: 0 }, selected: null, seed: seed >>> 0 || 1, lastSpin: null, pending: null };
}

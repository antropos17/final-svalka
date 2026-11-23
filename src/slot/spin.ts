import { economy, type Reels } from '../core/model';
export function draw(seed: number): { symbols: Reels; seedAfter: number } {
  let value = seed >>> 0;
  const symbols = Array.from({ length: 3 }, () => {
    value ^= value << 13; value ^= value >>> 17; value ^= value << 5; value >>>= 0;
    return economy.symbols[value >>> 30];
  }) as Reels;
  return { symbols, seedAfter: value };
}
export function payout(symbols: Reels): number {
  const count = new Set(symbols).size;
  return count === 1 ? economy.payouts.triple : count === 2 ? economy.payouts.pair : economy.payouts.different;
}

import { test, expect } from '@playwright/test';
test('new player selects, repairs, controls AUTO and reloads persisted world', async ({ page }) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto('/'); await expect(page.locator('#coins')).toHaveText('37');
  await page.locator('#project').click(); await page.getByRole('button',{name:'Начать проект'}).click();
  await expect(page.locator('#project-eyebrow')).toHaveText('ПРОЕКТ В РАБОТЕ');
  await page.locator('#spin').click(); await expect(page.locator('#stage')).toHaveText('ЗДЕСЬ УЖЕ ЕСТЬ ЖИЗНЬ',{timeout:7000});
  await expect(page.locator('#auto')).toBeEnabled();
  await page.locator('#project').click(); await page.getByRole('button',{name:'Начать проект'}).click();
  await page.locator('#auto').click(); await page.getByRole('button',{name:'СТОП авто включено'}).click();
  await expect(page.locator('#spin')).toBeEnabled({timeout:7000});
  const before = await page.locator('#coins').textContent();
  await page.reload(); await expect(page.locator('#coins')).toHaveText(before!); await expect(page.locator('#stage')).toHaveText('ЗДЕСЬ УЖЕ ЕСТЬ ЖИЗНЬ');
  await expect(page.locator('#project-eyebrow')).toHaveText('ПРОЕКТ В РАБОТЕ');
  await page.screenshot({path:'test-results/first-repair.png'}); expect(errors).toEqual([]);
});
test('rapid clicks grant a single committed spin and reload during presentation recovers it', async ({ page }) => {
  await page.goto('/'); await expect(page.locator('#spin')).toBeEnabled();
  await page.locator('#spin').evaluate((el: HTMLButtonElement) => { el.click(); el.click(); el.click(); });
  await page.waitForFunction(() => JSON.parse(localStorage.getItem('jtn.save.v1')!).sequence === 1);
  await page.reload(); await expect(page.locator('#spin')).toBeEnabled();
  const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('jtn.save.v1')!)); expect(stored.sequence).toBe(1); expect([40,45,60]).toContain(stored.coins);
});
test('corrupt save shows recovery without a silent initial grant', async ({page}) => {
  await page.addInitScript(() => localStorage.setItem('jtn.save.v1','invalid'));
  await page.goto('/'); await expect(page.getByRole('heading',{name:'Сохраним ваш прогресс'})).toBeVisible(); await expect(page.locator('#spin')).toBeDisabled();
  await page.keyboard.press('Escape'); await expect(page.getByRole('heading',{name:'Сохраним ваш прогресс'})).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('jtn.save.v1'))).toBe('invalid');
});
test('short portrait keeps primary controls on screen', async ({page}) => {
  await page.setViewportSize({width:360,height:640}); await page.goto('/'); await expect(page.locator('#spin')).toBeEnabled();
  const box = await page.locator('#spin').boundingBox(); expect(box!.y + box!.height).toBeLessThanOrEqual(640);
  await page.screenshot({path:'test-results/short-portrait.png'});
});
test('ordinary spins complete both explicitly selected projects', async ({page}) => {
  test.setTimeout(90000);
  await page.clock.install();
  await page.goto('/'); await expect(page.locator('#spin')).toBeEnabled();
  await page.locator('#project').click(); await page.getByRole('button',{name:'Начать проект'}).click();
  await page.locator('#spin').click(); await page.clock.fastForward(3500);
  await expect(page.locator('#stage')).toHaveText('ЗДЕСЬ УЖЕ ЕСТЬ ЖИЗНЬ');
  await page.locator('#project').click(); await page.getByRole('button',{name:'Начать проект'}).click();
  let completed = false;
  for (let i = 0; i < 200; i++) {
    await page.locator('#spin').click(); await page.clock.fastForward(3500);
    await expect(page.locator('#spin')).toBeEnabled();
    if ((await page.locator('#stage').textContent()) === 'ПЕРВЫЙ ОГОНЬ НЕОНА') { completed = true; break; }
  }
  expect(completed).toBe(true); const coins = await page.locator('#coins').textContent();
  await page.screenshot({path:'test-results/neon-stage.png'});
  await page.reload(); await expect(page.locator('#stage')).toHaveText('ПЕРВЫЙ ОГОНЬ НЕОНА'); await expect(page.locator('#coins')).toHaveText(coins!);
});

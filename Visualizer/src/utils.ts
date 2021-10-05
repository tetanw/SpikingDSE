// from: https://gist.github.com/cho45/9968462
export function formatSI(n) {
  const unitList = [
    "y",
    "z",
    "a",
    "f",
    "p",
    "n",
    "μ",
    "m",
    "",
    "k",
    "M",
    "G",
    "T",
    "P",
    "E",
    "Z",
    "Y",
  ];
  const zeroIndex = 8;
  const nn = n.toExponential(2).split(/e/);
  let u = Math.floor(+nn[1] / 3) + zeroIndex;
  if (u > unitList.length - 1) {
    u = unitList.length - 1;
  } else if (u < 0) {
    u = 0;
  }
  return (
    formatNumber(nn[0] * Math.pow(10, +nn[1] - (u - zeroIndex) * 3)) + " " +
    unitList[u]
  );
}

export function formatNumber(number: number): string {
  return number.toLocaleString("en-US", { maximumFractionDigits: 2 });
}

export function sum<T>(items: T[], fn: (item: T) => number): number {
  let cnt = 0;
  for (let i = 0; i < items.length; i++) {
    const item = items[i];
    cnt += fn(item);
  }
  return cnt;
}



from numpy import diag


def diagonal(startX, startY, width, height):
    values = []
    x = startX
    y = startY
    while x < width and y >= 0:
        values.append((x, y))
        x = x + 1
        y = y - 1
    return values 

def zigzag(width, height):
    values = []

    for y in range(0, height):
        values.extend(diagonal(0, y, width, height))
    
    for x in range(1, width):
        values.extend(diagonal(x, height - 1, width, height))

    return values

if __name__ == "__main__":
    print(diagonal(0, 0, 3, 3))
    print(diagonal(0, 1, 3, 3))
    print(diagonal(0, 2, 3, 3))
    print(diagonal(1, 2, 3, 3))
    print(diagonal(2, 2, 3, 3))

    print(zigzag(3, 3))
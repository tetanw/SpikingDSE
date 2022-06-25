def diagonal(startX, startY, width, height):
    values = []
    x = startX
    y = startY
    while x < width and y >= 0:
        values.append((x, y))
        x = x + 1
        y = y - 1
    return values

def matrix_zigzag(width, height):
    matrix = [[0 for _ in range(width)] for _ in range(height)]

    i = 0
    for y in range(0, height):
        for x, y in diagonal(0, y, width, height):
            matrix[y][x] = i
            i = i + 1

    for x in range(1, width):
        for x, y in diagonal(x, height - 1, width, height):
            matrix[y][x] = i
            i = i + 1
    return matrix

def matrix_vertical(width, height):
    matrix = [[0 for _ in range(width)] for _ in range(height)]

    i = 0
    for x in range(0, width):
        for y in range(0, height):
            matrix[y][x] = i
            i = i + 1

    return matrix

if __name__ == "__main__":
    print(diagonal(0, 0, 3, 3))
    print(diagonal(0, 1, 3, 3))
    print(diagonal(0, 2, 3, 3))
    print(diagonal(1, 2, 3, 3))
    print(diagonal(2, 2, 3, 3))

    def print_matrix(A):
        print('\n'.join([''.join(['{:4}'.format(item) for item in row])
                        for row in A]))
    
    a1 = matrix_zigzag(3, 3)
    print_matrix(a1)

    a2 = matrix_vertical(3, 3)
    print_matrix(a2)



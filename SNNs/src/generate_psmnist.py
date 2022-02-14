"""
The function used to generate permuted sequencial mnist dataset.
permute = np.random.permutation(784)
"""

import keras
import numpy as np
import matplotlib.pyplot as plt

def apply_permutation(data, permuteMatrix):
    b, c, r = data.shape
    sdata = data.reshape(b, c*r)
    new_data = np.zeros_like(sdata)
    for i in range(len(sdata)):
        tmp = sdata[i]
        new_data[i, :] = tmp[permuteMatrix]
    return new_data

(X_train, y_train), (X_test, y_test) = keras.datasets.mnist.load_data()
permute = np.random.permutation(784)
X_train_ps = apply_permutation(X_train, permute)
X_test_ps = apply_permutation(X_test, permute)

np.savez_compressed("data/psminst.npz", train_x=X_train_ps, train_y=y_train, test_x=X_test_ps, test_y=y_test)

print("X_train shape: ", X_train_ps.shape)
print("X_test shape: ", X_test_ps.shape)
print("Y_train shape: ", y_train.shape)
print("Y_test shape: ", y_test.shape)
plt.subplot(131)
plt.imshow(X_train[1].reshape(28, 28))
plt.gca().set_title('original')

plt.subplot(132)
plt.imshow(X_train_ps[1].reshape(28, 28))
plt.gca().set_title('permuted')

plt.subplot(133)
plt.imshow(permute.reshape(28, 28))
plt.gca().set_title('permute Matrix')
plt.show()

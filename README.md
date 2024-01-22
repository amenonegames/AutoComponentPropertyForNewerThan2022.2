# AutoComponentProperty

Monobehaviourでの、GetComponentを自動実装するIncremental Source Generatorです。
具体的には以下コードが

```
public class Hoge  : MonoBehaviour
{
        private Rigidbody _myRigidbody;
        private Rigidbody MyRigidbody => _myRigidbody is null ? (_myRigidbody = GetComponent<Rigidbody>()) : _myRigidbody;
}
```
次のように書けるようになります。

```
public partial class Hoge  : MonoBehaviour
{
        [CompoProp]private Rigidbody _rigidbody;
}
```
また、引数にGetFrom.Parentと、GetFrom.Childrenが指定可能です。
Parentを選ぶとGetComponentInParent()を、Childrenを選ぶとGetComponentInChildrenをそれぞれ利用します。
宣言の型を配列型にすると、いずれもGetComponents系のメソッドとなり、複数の要素を配列で受け付けます。

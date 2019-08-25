using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeSurgeon
{
    public interface ITypeSignature<out TSymbol> : IResolvable<TSymbol>, IEquatable<ITypeSignature<TypeSig>> where TSymbol : TypeSig { }

    public interface ILeafSignature<out TSymbol> : ITypeSignature<TSymbol>, IEquatable<ILeafSignature<LeafSig>> where TSymbol : LeafSig { }

    public interface ITypeDefOrRefSignature<out TSymbol> : ILeafSignature<TSymbol>, IEquatable<ITypeDefOrRefSignature<TypeDefOrRefSig>> where TSymbol : TypeDefOrRefSig
    {
        ITypeReference<ITypeDefOrRef> Type { get; }
    }

    public abstract class AbstractTypeDefOrRefSignature<TSymbol> : ITypeDefOrRefSignature<TSymbol> where TSymbol : TypeDefOrRefSig
    {
        public ITypeReference<ITypeDefOrRef> Type { get; }

        private protected AbstractTypeDefOrRefSignature(ITypeReference<ITypeDefOrRef> type) => Type = type;

        public abstract TSymbol Resolve(ISearchContext context);
        bool IEquatable<ITypeSignature<TypeSig>>.Equals(ITypeSignature<TypeSig> other) => Equals(other as ITypeDefOrRefSignature<TypeDefOrRefSig>);
        bool IEquatable<ILeafSignature<LeafSig>>.Equals(ILeafSignature<LeafSig> other) => Equals(other as ITypeDefOrRefSignature<TypeDefOrRefSig>);
        public virtual bool Equals(ITypeDefOrRefSignature<TypeDefOrRefSig> other) => !(other is null) && Type.Compare(other.Type);
    }

    public sealed class CorLibTypeSignature : AbstractTypeDefOrRefSignature<CorLibTypeSig>, IEquatable<CorLibTypeSignature>
    {
        public ElementType ElementType { get; }

        public CorLibTypeSignature(ITypeReference<ITypeDefOrRef> type, ElementType elementType) : base(type) => ElementType = elementType;
        
        public override CorLibTypeSig Resolve(ISearchContext context) => new CorLibTypeSig(context.Get(Type), ElementType);

        public override bool Equals(ITypeDefOrRefSignature<TypeDefOrRefSig> other) => Equals(other as CorLibTypeSignature);
        public bool Equals(CorLibTypeSignature other) => base.Equals(other) && ElementType == other.ElementType;
    }

    public interface IClassOrValueTypeSignature<out TSymbol> : ITypeDefOrRefSignature<TSymbol>, IEquatable<IClassOrValueTypeSignature<ClassOrValueTypeSig>> where TSymbol : ClassOrValueTypeSig { }

    public sealed class ValueTypeSignature : AbstractTypeDefOrRefSignature<ValueTypeSig>, IClassOrValueTypeSignature<ValueTypeSig>, IEquatable<ValueTypeSignature>
    {
        public ValueTypeSignature(ITypeReference<ITypeDefOrRef> type) : base(type) { }

        public override ValueTypeSig Resolve(ISearchContext context) => new ValueTypeSig(context.Get(Type));

        public override bool Equals(ITypeDefOrRefSignature<TypeDefOrRefSig> other) => Equals(other as ValueTypeSignature);
        public bool Equals(IClassOrValueTypeSignature<ClassOrValueTypeSig> other) => Equals(other as ValueTypeSignature);
        public bool Equals(ValueTypeSignature other) => base.Equals(other);
    }

    public sealed class ClassSignature : AbstractTypeDefOrRefSignature<ClassSig>, IClassOrValueTypeSignature<ClassSig>, IEquatable<ClassSignature>
    {
        public ClassSignature(ITypeReference<ITypeDefOrRef> type) : base(type) { }

        public override ClassSig Resolve(ISearchContext context) => new ClassSig(context.Get(Type));

        public override bool Equals(ITypeDefOrRefSignature<TypeDefOrRefSig> other) => Equals(other as ClassSignature);
        public bool Equals(IClassOrValueTypeSignature<ClassOrValueTypeSig> other) => Equals(other as ClassSignature);
        public bool Equals(ClassSignature other) => base.Equals(other);
    }

    public interface IGenericSignature<out TSymbol> : ILeafSignature<TSymbol>, IEquatable<IGenericSignature<GenericSig>> where TSymbol : GenericSig
    {
        bool IsTypeVar { get; }
        uint Number { get; }
        IMemberReference<ITypeOrMethodDef> GenericParamProvider { get; }
    }

    public abstract class AbstractGenericSignature<TSymbol> : IGenericSignature<TSymbol> where TSymbol : GenericSig
    {
        public abstract bool IsTypeVar { get; }
        public uint Number { get; }
        public IMemberReference<ITypeOrMethodDef> GenericParamProvider { get; private set; }

        private protected AbstractGenericSignature(uint number, IMemberReference<ITypeOrMethodDef> genericParamProvider)
        {
            Number = number;
            GenericParamProvider = genericParamProvider;
        }

        private protected AbstractGenericSignature(uint number, Action<Action<IMemberReference<ITypeOrMethodDef>>> genericParamProvider)
        {
            Number = number;
            genericParamProvider(p => GenericParamProvider = p);
        }

        public abstract TSymbol Resolve(ISearchContext context);

        public bool Equals(ITypeSignature<TypeSig> other) => Equals(other as IGenericSignature<GenericSig>);
        public bool Equals(ILeafSignature<LeafSig> other) => Equals(other as IGenericSignature<GenericSig>);
        public virtual bool Equals(IGenericSignature<GenericSig> other) => other != null && IsTypeVar == other.IsTypeVar && Number == other.Number && GenericParamProvider.Equals(other.GenericParamProvider);
    }

    public sealed class GenericVarSignature : AbstractGenericSignature<GenericVar>, IEquatable<GenericVarSignature>
    {
        public override bool IsTypeVar => true;
        public new TypeModification GenericParamProvider { get; }

        public GenericVarSignature(uint number, TypeModification genericParamProvider) : base(number, genericParamProvider) => GenericParamProvider = genericParamProvider;

        public override GenericVar Resolve(ISearchContext context) => new GenericVar(Number, null);//context.Get(GenericParamProvider));

        public override bool Equals(IGenericSignature<GenericSig> other) => Equals(other as GenericVarSignature);
        public bool Equals(GenericVarSignature other) => base.Equals(other);
    }

    public sealed class GenericMVarSignature : AbstractGenericSignature<GenericMVar>, IEquatable<GenericMVarSignature>
    {
        public override bool IsTypeVar => false;
        public new MethodModification GenericParamProvider => base.GenericParamProvider as MethodModification;

        public GenericMVarSignature(uint number, MethodModification genericParamProvider) : base(number, genericParamProvider) { }
        public GenericMVarSignature(uint number, Action<Action<MethodModification>> genericParamProvider) : base(number, genericParamProvider) { }

        public override GenericMVar Resolve(ISearchContext context) => new GenericMVar(Number, null);//context.Get(GenericParamProvider));

        public override bool Equals(IGenericSignature<GenericSig> other) => Equals(other as GenericMVarSignature);
        public bool Equals(GenericMVarSignature other) => base.Equals(other);
    }

    public sealed class SentinelSignature : ILeafSignature<SentinelSig>, IEquatable<SentinelSignature>
    {
        public SentinelSig Resolve(ISearchContext context) => new SentinelSig();

        public bool Equals(ITypeSignature<TypeSig> other) => Equals(other as SentinelSignature);
        public bool Equals(ILeafSignature<LeafSig> other) => Equals(other as SentinelSignature);
        public bool Equals(SentinelSignature other) => !(other is null);
    }

    public sealed class FnPtrSignature : ILeafSignature<FnPtrSig>, IEquatable<FnPtrSignature>
    {
        public ICallSignature<CallingConventionSig> Signature { get; }

        public FnPtrSignature(ICallSignature<CallingConventionSig> signature) => Signature = signature;

        public FnPtrSig Resolve(ISearchContext context) => new FnPtrSig(context.Get(Signature));

        public bool Equals(ITypeSignature<TypeSig> other) => Equals(other as FnPtrSignature);
        public bool Equals(ILeafSignature<LeafSig> other) => Equals(other as FnPtrSignature);
        public bool Equals(FnPtrSignature other) => !(other is null) && Signature.Compare(other.Signature);
    }

    public sealed class GenericInstSignature : ILeafSignature<GenericInstSig>, IEquatable<GenericInstSignature>
    {
        public IClassOrValueTypeSignature<ClassOrValueTypeSig> GenericType { get; }
        public IEnumerable<ITypeSignature<TypeSig>> GenArgs { get; }

        public GenericInstSignature(IClassOrValueTypeSignature<ClassOrValueTypeSig> genericType, IEnumerable<ITypeSignature<TypeSig>> genArgs)
        {
            GenericType = genericType;
            GenArgs = genArgs.ToList();
        }

        public GenericInstSig Resolve(ISearchContext context) => new GenericInstSig(context.Get(GenericType), GenArgs?.Select(context.Get).ToList());

        public bool Equals(ITypeSignature<TypeSig> other) => Equals(other as GenericInstSignature);
        public bool Equals(ILeafSignature<LeafSig> other) => Equals(other as GenericInstSignature);
        public bool Equals(GenericInstSignature other) => !(other is null) && GenericType.Compare(other.GenericType) && GenArgs.Compare(other.GenArgs);
    }

    public interface INonLeafSignature<out TSymbol> : ITypeSignature<TSymbol>, IEquatable<INonLeafSignature<NonLeafSig>> where TSymbol : NonLeafSig
    {
        ITypeSignature<TypeSig> Next { get; }
    }

    public abstract class AbstractNonLeafSignature<TSymbol> : INonLeafSignature<TSymbol> where TSymbol : NonLeafSig
    {
        public ITypeSignature<TypeSig> Next { get; }

        private protected AbstractNonLeafSignature(ITypeSignature<TypeSig> next) => Next = next;

        public abstract TSymbol Resolve(ISearchContext context);

        public bool Equals(ITypeSignature<TypeSig> other) => Equals(other as INonLeafSignature<NonLeafSig>);
        public virtual bool Equals(INonLeafSignature<NonLeafSig> other) => !(other is null) && Next.Compare(other.Next);
    }

    public sealed class PtrSignature : AbstractNonLeafSignature<PtrSig>, IEquatable<PtrSignature>
    {
        public PtrSignature(ITypeSignature<TypeSig> next) : base(next) { }

        public override PtrSig Resolve(ISearchContext context) => new PtrSig(context.Get(Next));

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as PtrSignature);
        public bool Equals(PtrSignature other) => base.Equals(other);
    }

    public sealed class ByRefSignature : AbstractNonLeafSignature<ByRefSig>, IEquatable<ByRefSignature>
    {
        public ByRefSignature(ITypeSignature<TypeSig> next) : base(next) { }

        public override ByRefSig Resolve(ISearchContext context) => new ByRefSig(context.Get(Next));

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as ByRefSignature);
        public bool Equals(ByRefSignature other) => base.Equals(other);
    }

    public interface IArrayBaseSignature<out TSymbol> : INonLeafSignature<TSymbol>, IEquatable<IArrayBaseSignature<ArraySigBase>> where TSymbol : ArraySigBase { }

    public sealed class ArraySignature : AbstractNonLeafSignature<ArraySig>, IArrayBaseSignature<ArraySig>, IEquatable<ArraySignature>
    {
        private uint Rank { get; }
        private IEnumerable<uint> Sizes { get; }
        private IEnumerable<int> LowerBounds { get; }

        public ArraySignature(ITypeSignature<TypeSig> next, uint rank, IEnumerable<uint> sizes, IEnumerable<int> lowerBounds) : base(next)
        {
            Rank = rank;
            Sizes = sizes;
            LowerBounds = lowerBounds;
        }

        public override ArraySig Resolve(ISearchContext context) => new ArraySig(context.Get(Next), Rank, Sizes, LowerBounds);

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as ArraySignature);
        public bool Equals(IArrayBaseSignature<ArraySigBase> other) => Equals(other as ArraySignature);
        public bool Equals(ArraySignature other) => base.Equals(other) && Rank == other.Rank && Sizes.Compare(other.Sizes) && LowerBounds.Compare(other.LowerBounds);
    }

    public sealed class SZArraySignature : AbstractNonLeafSignature<SZArraySig>, IArrayBaseSignature<SZArraySig>, IEquatable<SZArraySignature>
    {
        public SZArraySignature(ITypeSignature<TypeSig> next) : base(next) { }

        public override SZArraySig Resolve(ISearchContext context) => new SZArraySig(context.Get(Next));

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as SZArraySignature);
        public bool Equals(IArrayBaseSignature<ArraySigBase> other) => Equals(other as SZArraySignature);
        public bool Equals(SZArraySignature other) => base.Equals(other);
    }

    public interface IModifierSignature<out TSymbol> : INonLeafSignature<TSymbol>, IEquatable<IModifierSignature<ModifierSig>> where TSymbol : ModifierSig
    {
        ITypeReference<ITypeDefOrRef> Modifier { get; }
    }

    public abstract class AbstractModifierSignature<TSymbol> : AbstractNonLeafSignature<TSymbol>, IModifierSignature<TSymbol> where TSymbol : ModifierSig
    {
        public ITypeReference<ITypeDefOrRef> Modifier { get; }

        private protected AbstractModifierSignature(ITypeReference<ITypeDefOrRef> modifier, ITypeSignature<TypeSig> next) : base(next) => Modifier = modifier;

        public sealed override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as IModifierSignature<ModifierSig>);
        public virtual bool Equals(IModifierSignature<ModifierSig> other) => base.Equals(other) && Modifier.Compare(other.Modifier);
    }

    public sealed class CModReqdSignature : AbstractModifierSignature<CModReqdSig>, IEquatable<CModReqdSignature>
    {
        public CModReqdSignature(ITypeReference<ITypeDefOrRef> modifier, ITypeSignature<TypeSig> next) : base(modifier, next) { }

        public override CModReqdSig Resolve(ISearchContext context) => new CModReqdSig(context.Get(Modifier), context.Get(Next));

        public override bool Equals(IModifierSignature<ModifierSig> other) => Equals(other as CModReqdSignature);
        public bool Equals(CModReqdSignature other) => base.Equals(other);
    }

    public sealed class CModOptSignature : AbstractModifierSignature<CModOptSig>, IEquatable<CModOptSignature>
    {
        public CModOptSignature(ITypeReference<ITypeDefOrRef> modifier, ITypeSignature<TypeSig> next) : base(modifier, next) { }

        public override CModOptSig Resolve(ISearchContext context) => new CModOptSig(context.Get(Modifier), context.Get(Next));

        public override bool Equals(IModifierSignature<ModifierSig> other) => Equals(other as CModOptSignature);
        public bool Equals(CModOptSignature other) => base.Equals(other);
    }

    public sealed class PinnedSignature : AbstractNonLeafSignature<PinnedSig>, IEquatable<PinnedSignature>
    {
        public PinnedSignature(ITypeSignature<TypeSig> next) : base(next) { }

        public override PinnedSig Resolve(ISearchContext context) => new PinnedSig(context.Get(Next));

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as PinnedSignature);
        public bool Equals(PinnedSignature other) => base.Equals(other);
    }

    public sealed class ValueArraySignature : AbstractNonLeafSignature<ValueArraySig>, IEquatable<ValueArraySignature>
    {
        public uint Size { get; }

        public ValueArraySignature(ITypeSignature<TypeSig> next, uint size) : base(next) => Size = size;

        public override ValueArraySig Resolve(ISearchContext context) => new ValueArraySig(context.Get(Next), Size);

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as ValueArraySignature);
        public bool Equals(ValueArraySignature other) => base.Equals(other) && Size == other.Size;
    }

    public sealed class ModuleSignature : AbstractNonLeafSignature<ModuleSig>, IEquatable<ModuleSignature>
    {
        public uint Index { get; }

        public ModuleSignature(uint index, ITypeSignature<TypeSig> next) : base(next) => Index = index;

        public override ModuleSig Resolve(ISearchContext context) => new ModuleSig(Index, context.Get(Next));

        public override bool Equals(INonLeafSignature<NonLeafSig> other) => Equals(other as ModuleSignature);
        public bool Equals(ModuleSignature other) => base.Equals(other) && Index == other.Index;
    }
}
